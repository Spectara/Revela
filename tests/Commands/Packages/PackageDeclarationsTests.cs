using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Commands;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Integration")]
public sealed class PackageDeclarationsTests
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task RemoveAsync_QueuedAfterSectionDeletion_DoesNotResurrectDependency()
    {
        const string packageId = "Spectara.Revela.Plugins.Fixture";
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = PackagesSection(new JsonObject { [packageId] = "1.0.0" }),
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
        var service = new PackageDeclarations(forwardingConfig, Substitute.For<IGlobalConfigManager>(), NullLogger<PackageDeclarations>.Instance);
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
            sectionDeletion = configService.UpdateProjectConfigAsync(new JsonObject { ["dependencies"] = null });
            removal = service.RemoveAsync(packageId.ToUpperInvariant(), CancellationToken.None);
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
        Assert.IsFalse(saved.Any(section => string.Equals(section.Key, "dependencies", StringComparison.OrdinalIgnoreCase)));
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.IsEmpty(configuration.GetSection("dependencies:packages").GetChildren());
        Assert.IsEmpty(reopened.GetSection("dependencies:packages").GetChildren());
        Assert.IsEmpty(scanner.GetDependencies());
        using var reopenedScannerProvider = scannerServices.AddSingleton<IConfiguration>(reopened).BuildServiceProvider();
        Assert.IsEmpty(reopenedScannerProvider.GetRequiredService<IDependencyScanner>().GetDependencies());
        Assert.AreEqual("preserved", reopened["settings:sibling"]);
        Assert.AreEqual("committed", reopened["settings:first"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    public async Task DeclareAsync_DeclaredCaseSection_UpdatesExistingEntryAndReloadsProvider()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = new { Packages = new { MixedCasePackage = "1.0.0" } }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);

        await service.DeclareAsync("MixedCasePackage", "2.0.0", CancellationToken.None);

        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("2.0.0", reopened["dependencies:packages:mixedcasepackage"]);
        Assert.AreEqual("2.0.0", host.Services.GetRequiredService<IConfiguration>()["dependencies:packages:mixedcasepackage"]);
        Assert.HasCount(1, reopened.GetSection("dependencies:packages").GetChildren());
    }

    [TestMethod]
    public async Task DeclareAsync_NoDependenciesSection_WritesNewShapeWithoutLegacyMaps()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Theme = new { Name = "Lumina" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);

        await service.DeclareAsync("Acme.Revela.Watermark", "1.0.0-beta.2", CancellationToken.None);

        var saved = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(saved);
        Assert.AreEqual("1.0.0-beta.2", saved["dependencies"]!["packages"]!["Acme.Revela.Watermark"]!.GetValue<string>());
        Assert.IsFalse(saved.ContainsKey("plugins"));
        Assert.IsFalse(saved.ContainsKey("themes"));
    }

    [TestMethod]
    public async Task DeclareAndRemoveAsync_MixedCasePackage_PreservesSpellingNullVersionsAndUnknownData()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = PackagesSection(new JsonObject
            {
                ["MixedCasePackage"] = "1.0.0",
                ["UnpinnedPackage"] = null,
                ["OtherPackage"] = "3.0.0",
                ["FutureEntry"] = new JsonObject { ["Enabled"] = true }
            }),
            UnknownSettings = new { MixedCase = new JsonArray("one", "two"), Count = 42 }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);
        var expected = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(expected);

        await service.DeclareAsync("mixedcasepackage", "2.0.0", CancellationToken.None);
        expected["Dependencies"]!["Packages"]!["MixedCasePackage"] = "2.0.0";
        Assert.IsTrue(JsonNode.DeepEquals(expected, await configService.ReadProjectConfigAsync()));
        Assert.AreEqual("2.0.0", configuration["dependencies:packages:mixedcasepackage"]);

        await service.RemoveAsync("MIXEDCASEPACKAGE", CancellationToken.None);
        expected["Dependencies"]!["Packages"]!.AsObject().Remove("MixedCasePackage");
        Assert.IsTrue(JsonNode.DeepEquals(expected, await configService.ReadProjectConfigAsync()));
        Assert.IsNull(configuration["dependencies:packages:mixedcasepackage"]);
        Assert.AreEqual("3.0.0", configuration["dependencies:packages:otherpackage"]);
        using (var stream = File.OpenRead(project.ProjectJsonPath))
        using (var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build())
        {
            Assert.HasCount(3, reopened.GetSection("dependencies:packages").GetChildren());
            Assert.IsNull(reopened["dependencies:packages:mixedcasepackage"]);
            Assert.IsNull(reopened["dependencies:packages:unpinnedpackage"]);
            Assert.AreEqual("3.0.0", reopened["dependencies:packages:otherpackage"]);
            Assert.AreEqual("True", reopened["dependencies:packages:futureentry:enabled"]);
            Assert.AreEqual("two", reopened["unknownsettings:mixedcase:1"]);
        }

        await service.RemoveAsync("UNPINNEDPACKAGE", CancellationToken.None);
        expected["Dependencies"]!["Packages"]!.AsObject().Remove("UnpinnedPackage");
        Assert.IsTrue(JsonNode.DeepEquals(expected, await configService.ReadProjectConfigAsync()));
        Assert.HasCount(2, configuration.GetSection("dependencies:packages").GetChildren());
    }

    [TestMethod]
    [DataRow("{ }")]
    [DataRow(/*lang=json,strict*/ "{\"Dependencies\":{\"Packages\":{}}}")]
    [DataRow(/*lang=json,strict*/ "{\"Dependencies\":{\"Packages\":{\"OtherPackage\":null}}}")]
    [DataRow(/*lang=json,strict*/ "{\"Dependencies\":{\"Packages\":null},\"Unknown\":42}")]
    public async Task RemoveAsync_UnknownPackage_DoesNotRewriteOrInsertNullEntry(string json)
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, json);
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);
        var reload = host.Services.GetRequiredService<IConfiguration>().GetReloadToken();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await service.RemoveAsync("UnknownPackage", CancellationToken.None);

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.IsFalse(reload.HasChanged);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    public async Task DeclareAndRemoveAsync_OutsideProject_WritesOnlyRevelaJson()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        File.Delete(project.ProjectJsonPath);
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services, globalConfig);
        var reload = host.Services.GetRequiredService<IConfiguration>().GetReloadToken();

        await service.DeclareAsync("Package", "1.0.0", CancellationToken.None);
        await service.RemoveAsync("Package", CancellationToken.None);

        await globalConfig.Received(1).AddPackageAsync("Package", "1.0.0", Arg.Any<CancellationToken>());
        _ = await globalConfig.Received(1).RemovePackageAsync("Package", Arg.Any<CancellationToken>());
        Assert.IsFalse(File.Exists(project.ProjectJsonPath));
        Assert.IsFalse(reload.HasChanged);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DeclareOrRemoveAsync_PreCanceled_LeavesProjectUnchanged(bool remove)
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = new { Packages = new { MixedCasePackage = "1.0.0" } }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => remove
            ? service.RemoveAsync("mixedcasepackage", cancellation.Token)
            : service.DeclareAsync("mixedcasepackage", "2.0.0", cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("1.0.0", host.Services.GetRequiredService<IConfiguration>()["dependencies:packages:mixedcasepackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DeclareOrRemoveAsync_MalformedOriginal_PropagatesFailureWithoutWriting(bool remove)
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = new { Packages = new { MixedCasePackage = "1.0.0" } }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);
        await File.WriteAllTextAsync(project.ProjectJsonPath, "{ malformed json");
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsAsync<JsonException>(() => remove
            ? service.RemoveAsync("mixedcasepackage", CancellationToken.None)
            : service.DeclareAsync("mixedcasepackage", "2.0.0", CancellationToken.None));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("1.0.0", host.Services.GetRequiredService<IConfiguration>()["dependencies:packages:mixedcasepackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DeclareOrRemoveAsync_ProviderInvalidOriginal_PropagatesFailureWithoutWriting(bool remove)
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = new { Packages = new { MixedCasePackage = "1.0.0" } }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);
        await File.WriteAllTextAsync(project.ProjectJsonPath, /*lang=json,strict*/ """
            {"Dependencies":{"Packages":{"MixedCasePackage":"original"}},"dependencies":{"packages":{"mixedcasepackage":"collision"}}}
            """);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsExactlyAsync<FormatException>(() => remove
            ? service.RemoveAsync("mixedcasepackage", CancellationToken.None)
            : service.DeclareAsync("mixedcasepackage", "2.0.0", CancellationToken.None));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("1.0.0", host.Services.GetRequiredService<IConfiguration>()["dependencies:packages:mixedcasepackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DeclareOrRemoveAsync_AmbiguousCaseSplitSection_LeavesBothSectionsUnchanged(bool remove)
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = new { Packages = new { MixedCasePackage = "1.0.0" } },
            dependencies = new { packages = new { OtherPackage = "3.0.0" } }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => remove
            ? service.RemoveAsync("mixedcasepackage", CancellationToken.None)
            : service.DeclareAsync("mixedcasepackage", "2.0.0", CancellationToken.None));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        Assert.AreEqual("1.0.0", configuration["dependencies:packages:mixedcasepackage"]);
        Assert.AreEqual("3.0.0", configuration["dependencies:packages:otherpackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    public async Task DeclareAsync_DistinctServicesShareWriter_SerializesWithOrdinaryUpdatesThroughReload()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = PackagesSection(new JsonObject { ["UnpinnedPackage"] = null }),
            Settings = new { Sibling = "preserved" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var firstService = new PackageDeclarations(configService, Substitute.For<IGlobalConfigManager>(), NullLogger<PackageDeclarations>.Instance);
        var secondService = new PackageDeclarations(configService, Substitute.For<IGlobalConfigManager>(), NullLogger<PackageDeclarations>.Instance);
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
            var firstPackageUpdate = firstService.DeclareAsync("FirstPackage", "1.0.0", CancellationToken.None);
            var secondPackageUpdate = secondService.DeclareAsync("SecondPackage", "2.0.0", CancellationToken.None);
            var settingsUpdate = configService.UpdateProjectConfigAsync(
                new JsonObject { ["settings"] = new JsonObject { ["second"] = "queued" } });
            queuedUpdates = Task.WhenAll(firstPackageUpdate, secondPackageUpdate, settingsUpdate);
            using var cancellation = new CancellationTokenSource();
            var canceledUpdate = secondService.DeclareAsync("CanceledPackage", "3.0.0", cancellation.Token);
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
            Assert.AreEqual("1.0.0", provider["dependencies:packages:firstpackage"]);
            Assert.AreEqual("2.0.0", provider["dependencies:packages:secondpackage"]);
            Assert.IsNull(provider["dependencies:packages:canceledpackage"]);
            Assert.HasCount(3, provider.GetSection("dependencies:packages").GetChildren());
            Assert.AreEqual("committed", provider["settings:first"]);
            Assert.AreEqual("queued", provider["settings:second"]);
            Assert.AreEqual("preserved", provider["settings:sibling"]);
        }

        var saved = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(saved);
        Assert.IsTrue(saved["Dependencies"]!["Packages"]!.AsObject().ContainsKey("UnpinnedPackage"));
        Assert.IsNull(saved["Dependencies"]!["Packages"]!["UnpinnedPackage"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".project.json.*.tmp"));
    }

    [TestMethod]
    public async Task PinAsync_DeclaredInProject_RecordsExactVersion()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = PackagesSection(new JsonObject { ["MixedCasePackage"] = "latest" })
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services);

        var pinned = await service.PinAsync("mixedcasepackage", "1.2.0", CancellationToken.None);

        Assert.IsTrue(pinned);
        Assert.AreEqual("1.2.0", host.Services.GetRequiredService<IConfiguration>()["dependencies:packages:mixedcasepackage"]);
    }

    [TestMethod]
    public async Task PinAsync_NotDeclaredInProject_LeavesProjectUnchanged()
    {
        using var project = TestProject.Create(projectBuilder => projectBuilder.WithProjectJson(new
        {
            Dependencies = PackagesSection(new JsonObject { ["OtherPackage"] = "1.0.0" })
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var service = ActivatorUtilities.CreateInstance<PackageDeclarations>(host.Services, globalConfig);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        var pinned = await service.PinAsync("GlobalOnlyPackage", "1.2.0", CancellationToken.None);

        Assert.IsFalse(pinned);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.IsEmpty(globalConfig.ReceivedCalls());
    }

    private static JsonObject PackagesSection(JsonObject packages) => new() { ["Packages"] = packages };
}
