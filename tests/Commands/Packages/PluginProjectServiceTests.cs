using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Commands;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Core;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Integration")]
public sealed class PluginProjectServiceTests
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task RemovePluginAsync_QueuedAfterSectionDeletion_DoesNotResurrectDependency()
    {
        const string packageId = "Spectara.Revela.Plugins.Fixture";
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Plugins = new JsonObject { [packageId] = "1.0.0" },
            Settings = new { Sibling = "preserved" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var scannerServices = new ServiceCollection();
        scannerServices.AddSingleton(configuration);
        scannerServices.AddLogging();
        scannerServices.AddRevelaConfigSections();
        scannerServices.AddSingleton<IDependencyScanner, DependencyScanner>();
        using var scannerProvider = scannerServices.BuildServiceProvider();
        var scanner = scannerProvider.GetRequiredService<IDependencyScanner>();
        Assert.HasCount(1, scanner.GetDependencies());
        Assert.AreEqual(packageId, scanner.GetDependencies()[0].PackageId);
        var reloadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var removalQueued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var forwardingConfig = Substitute.For<IConfigService>();
        forwardingConfig.IsProjectInitialized().Returns(_ => configService.IsProjectInitialized());
        forwardingConfig.ProjectConfigPath.Returns(configService.ProjectConfigPath);
        forwardingConfig.ReadProjectConfigAsync(Arg.Any<CancellationToken>())
            .Returns(call => configService.ReadProjectConfigAsync(call.Arg<CancellationToken>()));
        forwardingConfig.UpdateProjectConfigAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var update = configService.UpdateProjectConfigAsync(call.Arg<JsonObject>(), call.Arg<CancellationToken>());
            removalQueued.TrySetResult();
            return update;
        });
        var service = new PluginProjectService(forwardingConfig, NullLogger<PluginProjectService>.Instance);
        using var registration = configuration.GetReloadToken().RegisterChangeCallback(_ =>
        {
            reloadEntered.TrySetResult();
            if (!releaseReload.Task.Wait(OperationTimeout))
            {
                throw new TimeoutException("The test did not release the configuration reload.");
            }
        }, null);
        var firstUpdate = Task.Run(() => configService.UpdateProjectConfigAsync(
            new JsonObject { ["settings"] = new JsonObject { ["first"] = "committed" } }));
        var sectionDeletion = Task.CompletedTask;
        var removal = Task.CompletedTask;

        try
        {
            await reloadEntered.Task.WaitAsync(OperationTimeout);
            sectionDeletion = configService.UpdateProjectConfigAsync(new JsonObject { ["plugins"] = null });
            removal = service.RemovePluginAsync(packageId.ToUpperInvariant(), CancellationToken.None);
            await removalQueued.Task.WaitAsync(OperationTimeout);
            Assert.IsFalse(sectionDeletion.IsCompleted);
            Assert.IsFalse(removal.IsCompleted);
        }
        finally
        {
            releaseReload.TrySetResult();
            await Task.WhenAll(firstUpdate, sectionDeletion, removal).WaitAsync(OperationTimeout);
        }

        var saved = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(saved);
        Assert.IsFalse(saved.Any(section => string.Equals(section.Key, "plugins", StringComparison.OrdinalIgnoreCase)));
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.IsEmpty(configuration.GetSection("plugins").GetChildren());
        Assert.IsEmpty(reopened.GetSection("plugins").GetChildren());
        Assert.IsEmpty(scanner.GetDependencies());
        using var reopenedScannerProvider = scannerServices.AddSingleton<IConfiguration>(reopened).BuildServiceProvider();
        Assert.IsEmpty(reopenedScannerProvider.GetRequiredService<IDependencyScanner>().GetDependencies());
        Assert.AreEqual("preserved", reopened["settings:sibling"]);
        Assert.AreEqual("committed", reopened["settings:first"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    public async Task AddPluginAsync_DeclaredCaseSection_UpdatesExistingEntryAndReloadsProvider()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Plugins = new { MixedCasePackage = "1.0.0" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PluginProjectService>(host.Services);

        await service.AddPluginAsync("MixedCasePackage", "2.0.0", CancellationToken.None);

        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("2.0.0", reopened["plugins:mixedcasepackage"]);
        Assert.AreEqual("2.0.0", host.Services.GetRequiredService<IConfiguration>()["plugins:mixedcasepackage"]);
        Assert.HasCount(1, reopened.GetSection("plugins").GetChildren());
    }

    [TestMethod]
    public async Task AddAndRemovePluginAsync_MixedCasePackage_PreservesSpellingNullVersionsAndUnknownData()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Plugins = new JsonObject
            {
                ["MixedCasePackage"] = "1.0.0",
                ["UnpinnedPackage"] = null,
                ["OtherPackage"] = "3.0.0",
                ["FutureEntry"] = new JsonObject { ["Enabled"] = true }
            },
            UnknownSettings = new { MixedCase = new JsonArray("one", "two"), Count = 42 }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var service = ActivatorUtilities.CreateInstance<PluginProjectService>(host.Services);
        var expected = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(expected);

        await service.AddPluginAsync("mixedcasepackage", "2.0.0", CancellationToken.None);
        expected["Plugins"]!["MixedCasePackage"] = "2.0.0";
        Assert.IsTrue(JsonNode.DeepEquals(expected, await configService.ReadProjectConfigAsync()));
        Assert.AreEqual("2.0.0", configuration["plugins:mixedcasepackage"]);

        await service.RemovePluginAsync("MIXEDCASEPACKAGE", CancellationToken.None);
        expected["Plugins"]!.AsObject().Remove("MixedCasePackage");
        Assert.IsTrue(JsonNode.DeepEquals(expected, await configService.ReadProjectConfigAsync()));
        Assert.IsNull(configuration["plugins:mixedcasepackage"]);
        Assert.AreEqual("3.0.0", configuration["plugins:otherpackage"]);
        using (var stream = File.OpenRead(project.ProjectJsonPath))
        using (var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build())
        {
            Assert.HasCount(3, reopened.GetSection("plugins").GetChildren());
            Assert.IsNull(reopened["plugins:mixedcasepackage"]);
            Assert.IsNull(reopened["plugins:unpinnedpackage"]);
            Assert.AreEqual("3.0.0", reopened["plugins:otherpackage"]);
            Assert.AreEqual("True", reopened["plugins:futureentry:enabled"]);
            Assert.AreEqual("two", reopened["unknownsettings:mixedcase:1"]);
        }

        await service.RemovePluginAsync("UNPINNEDPACKAGE", CancellationToken.None);
        expected["Plugins"]!.AsObject().Remove("UnpinnedPackage");
        Assert.IsTrue(JsonNode.DeepEquals(expected, await configService.ReadProjectConfigAsync()));
        Assert.HasCount(2, configuration.GetSection("plugins").GetChildren());
    }

    [TestMethod]
    [DataRow("{ }")]
    [DataRow(/*lang=json,strict*/ "{\"Plugins\":{}}")]
    [DataRow(/*lang=json,strict*/ "{\"Plugins\":{\"OtherPackage\":null}}")]
    [DataRow(/*lang=json,strict*/ "{\"Plugins\":null,\"Unknown\":42}")]
    public async Task RemovePluginAsync_UnknownPackage_DoesNotRewriteOrInsertNullEntry(string json)
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, json);
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PluginProjectService>(host.Services);
        var reload = host.Services.GetRequiredService<IConfiguration>().GetReloadToken();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await service.RemovePluginAsync("UnknownPackage", CancellationToken.None);

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.IsFalse(reload.HasChanged);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    public async Task AddAndRemovePluginAsync_OutsideProject_DoesNotCreateProjectFile()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        File.Delete(project.ProjectJsonPath);
        var service = ActivatorUtilities.CreateInstance<PluginProjectService>(host.Services);
        var reload = host.Services.GetRequiredService<IConfiguration>().GetReloadToken();

        await service.AddPluginAsync("Package", "1.0.0", CancellationToken.None);
        await service.RemovePluginAsync("Package", CancellationToken.None);

        Assert.IsFalse(File.Exists(project.ProjectJsonPath));
        Assert.IsFalse(reload.HasChanged);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddOrRemovePluginAsync_PreCanceled_LeavesProjectUnchanged(bool remove)
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Plugins = new { MixedCasePackage = "1.0.0" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PluginProjectService>(host.Services);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => remove
            ? service.RemovePluginAsync("mixedcasepackage", cancellation.Token)
            : service.AddPluginAsync("mixedcasepackage", "2.0.0", cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("1.0.0", host.Services.GetRequiredService<IConfiguration>()["plugins:mixedcasepackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddOrRemovePluginAsync_MalformedOriginal_PropagatesFailureWithoutWriting(bool remove)
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Plugins = new { MixedCasePackage = "1.0.0" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PluginProjectService>(host.Services);
        await File.WriteAllTextAsync(project.ProjectJsonPath, "{ malformed json");
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsAsync<JsonException>(() => remove
            ? service.RemovePluginAsync("mixedcasepackage", CancellationToken.None)
            : service.AddPluginAsync("mixedcasepackage", "2.0.0", CancellationToken.None));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("1.0.0", host.Services.GetRequiredService<IConfiguration>()["plugins:mixedcasepackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddOrRemovePluginAsync_ProviderInvalidOriginal_PropagatesFailureWithoutWriting(bool remove)
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Plugins = new { MixedCasePackage = "1.0.0" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PluginProjectService>(host.Services);
        await File.WriteAllTextAsync(project.ProjectJsonPath, /*lang=json,strict*/ """
            {"Plugins":{"MixedCasePackage":"original"},"plugins":{"mixedcasepackage":"collision"}}
            """);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsExactlyAsync<FormatException>(() => remove
            ? service.RemovePluginAsync("mixedcasepackage", CancellationToken.None)
            : service.AddPluginAsync("mixedcasepackage", "2.0.0", CancellationToken.None));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("1.0.0", host.Services.GetRequiredService<IConfiguration>()["plugins:mixedcasepackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddOrRemovePluginAsync_AmbiguousCaseSplitSection_LeavesBothSectionsUnchanged(bool remove)
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Plugins = new { MixedCasePackage = "1.0.0" },
            plugins = new { OtherPackage = "3.0.0" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PluginProjectService>(host.Services);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => remove
            ? service.RemovePluginAsync("mixedcasepackage", CancellationToken.None)
            : service.AddPluginAsync("mixedcasepackage", "2.0.0", CancellationToken.None));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        Assert.AreEqual("1.0.0", configuration["plugins:mixedcasepackage"]);
        Assert.AreEqual("3.0.0", configuration["plugins:otherpackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    public async Task AddPluginAsync_DistinctServicesShareWriter_SerializesWithOrdinaryUpdatesThroughReload()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Plugins = new JsonObject { ["UnpinnedPackage"] = null },
            Settings = new { Sibling = "preserved" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var firstService = new PluginProjectService(configService, NullLogger<PluginProjectService>.Instance);
        var secondService = new PluginProjectService(configService, NullLogger<PluginProjectService>.Instance);
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var reloadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = configuration.GetReloadToken().RegisterChangeCallback(_ =>
        {
            reloadEntered.TrySetResult();
            if (!releaseReload.Task.Wait(OperationTimeout))
            {
                throw new TimeoutException("The test did not release the configuration reload.");
            }
        }, null);
        var firstUpdate = Task.Run(() => configService.UpdateProjectConfigAsync(
            new JsonObject { ["settings"] = new JsonObject { ["first"] = "committed" } }));
        var queuedUpdates = Task.CompletedTask;

        try
        {
            await reloadEntered.Task.WaitAsync(OperationTimeout);
            var committed = await File.ReadAllBytesAsync(project.ProjectJsonPath);
            var firstPackageUpdate = firstService.AddPluginAsync("FirstPackage", "1.0.0", CancellationToken.None);
            var secondPackageUpdate = secondService.AddPluginAsync("SecondPackage", "2.0.0", CancellationToken.None);
            var settingsUpdate = configService.UpdateProjectConfigAsync(
                new JsonObject { ["settings"] = new JsonObject { ["second"] = "queued" } });
            queuedUpdates = Task.WhenAll(firstPackageUpdate, secondPackageUpdate, settingsUpdate);
            using var cancellation = new CancellationTokenSource();
            var canceledUpdate = secondService.AddPluginAsync("CanceledPackage", "3.0.0", cancellation.Token);
            var completedBeforeCancellation = canceledUpdate.IsCompleted;
            await cancellation.CancelAsync();

            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => canceledUpdate.WaitAsync(OperationTimeout));
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
            Assert.IsFalse(completedBeforeCancellation);
            Assert.IsFalse(firstPackageUpdate.IsCompleted);
            Assert.IsFalse(secondPackageUpdate.IsCompleted);
            Assert.IsFalse(settingsUpdate.IsCompleted);
            CollectionAssert.AreEqual(committed, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        }
        finally
        {
            releaseReload.TrySetResult();
            await Task.WhenAll(firstUpdate, queuedUpdates).WaitAsync(OperationTimeout);
        }

        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        foreach (var provider in new[] { configuration, reopened })
        {
            Assert.AreEqual("1.0.0", provider["plugins:firstpackage"]);
            Assert.AreEqual("2.0.0", provider["plugins:secondpackage"]);
            Assert.IsNull(provider["plugins:canceledpackage"]);
            Assert.HasCount(3, provider.GetSection("plugins").GetChildren());
            Assert.AreEqual("committed", provider["settings:first"]);
            Assert.AreEqual("queued", provider["settings:second"]);
            Assert.AreEqual("preserved", provider["settings:sibling"]);
        }

        var saved = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(saved);
        Assert.IsTrue(saved["Plugins"]!.AsObject().ContainsKey("UnpinnedPackage"));
        Assert.IsNull(saved["Plugins"]!["UnpinnedPackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }
}
