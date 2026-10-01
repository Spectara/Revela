using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Config;

[TestClass]
[TestCategory("Integration")]
public sealed class ConfigServiceTests
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    [DataRow("{}")]
    [DataRow(/*lang=json,strict*/ "{\"New\":\"old-scalar\"}")]
    [DataRow(/*lang=json,strict*/ "{\"New\":null}")]
    public async Task UpdateProjectConfigAsync_NewNestedObject_AppliesDeletionWithoutMutatingPatch(string originalJson)
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, originalJson);
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var updates = new JsonObject
        {
            ["New"] = new JsonObject
            {
                ["Deleted"] = null,
                ["Nested"] = new JsonObject { ["Added"] = "kept", ["Deleted"] = null },
                ["DeletionOnly"] = new JsonObject { ["Nested"] = new JsonObject { ["Deleted"] = null } },
                ["Empty"] = new JsonObject(),
                ["Values"] = new JsonArray(null, "replacement", null)
            }
        };
        var originalPatch = updates.DeepClone();

        await configService.UpdateProjectConfigAsync(updates);

        var saved = await configService.ReadProjectConfigAsync();
        var expected = JsonNode.Parse("""
            {"New":{"Nested":{"Added":"kept"},"Empty":{},"Values":[null,"replacement",null]}}
            """);
        Assert.IsTrue(JsonNode.DeepEquals(expected, saved));
        Assert.IsTrue(JsonNode.DeepEquals(originalPatch, updates));
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("kept", reopened["new:nested:added"]);
        Assert.HasCount(1, reopened.GetSection("new:nested").GetChildren());
        Assert.IsEmpty(reopened.GetSection("new:deletiononly").GetChildren());
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow(/*lang=json,strict*/ "{\"settings\":{\"value\":\"preserved\"}}")]
    [DataRow(/*lang=json,strict*/ "{\"Missing\":{\"Nested\":{\"Deleted\":null}}}")]
    [DataRow(/*lang=json,strict*/ "{\"RawNull\":{\"Deleted\":null}}")]
    [DataRow(/*lang=json,strict*/ "{\"settings\":{\"value\":{\"Deleted\":null}}}")]
    public async Task UpdateProjectConfigAsync_NoEffectiveChanges_PreservesBytesAndDoesNotReload(string patchJson)
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, /*lang=json*/ """
            {
                // Keep comments, whitespace, BOM and untouched literal nulls.
                "Settings": { "Value": "preserved", },
                "RawNull": null,
            }
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var reload = host.Services.GetRequiredService<IConfiguration>().GetReloadToken();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await configService.UpdateProjectConfigAsync(JsonNode.Parse(patchJson)!.AsObject());

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.IsFalse(reload.HasChanged);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow(/*lang=json,strict*/ "{\"Missing\":{\"Deleted\":null}}")]
    public async Task UpdateProjectConfigAsync_NoEffectiveChanges_StillValidatesOriginal(string patchJson)
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var reload = host.Services.GetRequiredService<IConfiguration>().GetReloadToken();
        await File.WriteAllTextAsync(project.ProjectJsonPath, /*lang=json,strict*/ """
            {"Paths":{"Source":"original"},"paths:source":"collision"}
            """);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsExactlyAsync<FormatException>(() => configService.UpdateProjectConfigAsync(
            JsonNode.Parse(patchJson)!.AsObject()));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.IsFalse(reload.HasChanged);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_CanceledWhileReloadInProgress_DoesNotAccessFileAndAllowsNextUpdate()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Settings = new { Sibling = "preserved" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        using var scope = host.Services.CreateScope();
        var configService = host.Services.GetRequiredService<IConfigService>();
        var secondWriter = scope.ServiceProvider.GetRequiredService<IConfigService>();
        Assert.AreSame(configService, secondWriter);
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

        try
        {
            await reloadEntered.Task.WaitAsync(OperationTimeout);
            var committed = await File.ReadAllBytesAsync(project.ProjectJsonPath);
            using var cancellation = new CancellationTokenSource();
            using (var exclusiveRead = new FileStream(project.ProjectJsonPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var queuedUpdate = secondWriter.UpdateProjectConfigAsync(
                    new JsonObject { ["settings"] = new JsonObject { ["canceled"] = "must-not-appear" } },
                    cancellation.Token);
                var completedBeforeCancellation = queuedUpdate.IsCompleted;
                await cancellation.CancelAsync();

                await Assert.ThrowsAsync<OperationCanceledException>(() => queuedUpdate.WaitAsync(OperationTimeout));
                Assert.IsFalse(completedBeforeCancellation, "The second writer must wait for reload to finish.");
            }

            CollectionAssert.AreEqual(committed, await File.ReadAllBytesAsync(project.ProjectJsonPath));
            Assert.IsFalse(firstUpdate.IsCompleted);
            Assert.IsEmpty(GetTempFiles(project.RootPath));
        }
        finally
        {
            releaseReload.TrySetResult();
            await firstUpdate.WaitAsync(OperationTimeout);
        }

        await secondWriter.UpdateProjectConfigAsync(
            new JsonObject { ["settings"] = new JsonObject { ["next"] = "succeeded" } }).WaitAsync(OperationTimeout);

        Assert.AreEqual("committed", configuration["settings:first"]);
        Assert.AreEqual("succeeded", configuration["settings:next"]);
        Assert.AreEqual("preserved", configuration["settings:sibling"]);
        Assert.IsNull(configuration["settings:canceled"]);
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("committed", reopened["settings:first"]);
        Assert.AreEqual("succeeded", reopened["settings:next"]);
        Assert.AreEqual("preserved", reopened["settings:sibling"]);
        Assert.IsNull(reopened["settings:canceled"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_DisposedDuringReload_CompletesActiveAndQueuedUpdates()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Settings = new { Sibling = "preserved" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
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
        var queuedUpdate = Task.CompletedTask;

        try
        {
            await reloadEntered.Task.WaitAsync(OperationTimeout);
            queuedUpdate = configService.UpdateProjectConfigAsync(
                new JsonObject { ["settings"] = new JsonObject { ["queued"] = "succeeded" } });
            ((IDisposable)configService).Dispose();

            Assert.IsFalse(firstUpdate.IsCompleted);
            Assert.IsFalse(queuedUpdate.IsCompleted);
        }
        finally
        {
            releaseReload.TrySetResult();
            await Task.WhenAll(firstUpdate, queuedUpdate).WaitAsync(OperationTimeout);
        }

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => configService.UpdateProjectConfigAsync(
            new JsonObject { ["settings"] = new JsonObject { ["rejected"] = "must-not-appear" } }));
        Assert.AreEqual("committed", configuration["settings:first"]);
        Assert.AreEqual("succeeded", configuration["settings:queued"]);
        Assert.AreEqual("preserved", configuration["settings:sibling"]);
        Assert.IsNull(configuration["settings:rejected"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_TargetBrieflyOpenByReader_RetriesAndWrites()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { Settings = new { Sibling = "untouched" } }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();

        // Same sharing mode as the JSON configuration provider's reload (no FileShare.Delete), which
        // blocks an atomic replace of project.json on Windows while it is open.
        await using var reader = new FileStream(project.ProjectJsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var releaseReader = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            await reader.DisposeAsync();
        });

        try
        {
            await configService.UpdateProjectConfigAsync(
                new JsonObject { ["plugins"] = new JsonObject { ["Package"] = "1.0.0" } }).WaitAsync(OperationTimeout);
        }
        finally
        {
            await releaseReader;
        }

        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("1.0.0", reopened["plugins:Package"]);
        Assert.AreEqual("untouched", reopened["settings:sibling"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_ConcurrentUpdates_PreservesEveryUniqueKeyAndSibling()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Plugins = new { Existing = "preserved" },
            Settings = new { Sibling = "untouched" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var packageNames = Enumerable.Range(0, 16)
            .Select(index => $"Package{index.ToString(CultureInfo.InvariantCulture)}")
            .ToArray();
        var updates = packageNames.Select(packageName => Task.Run(async () =>
        {
            await start.Task;
            await configService.UpdateProjectConfigAsync(
                new JsonObject { ["plugins"] = new JsonObject { [packageName] = "1.0.0" } });
        })).ToArray();

        start.SetResult();
        await Task.WhenAll(updates).WaitAsync(OperationTimeout);

        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        foreach (var packageName in packageNames)
        {
            Assert.AreEqual("1.0.0", configuration[$"plugins:{packageName}"]);
            Assert.AreEqual("1.0.0", reopened[$"plugins:{packageName}"]);
        }

        Assert.HasCount(packageNames.Length + 1, reopened.GetSection("plugins").GetChildren());
        Assert.AreEqual("preserved", configuration["plugins:existing"]);
        Assert.AreEqual("untouched", configuration["settings:sibling"]);
        Assert.AreEqual("preserved", reopened["plugins:existing"]);
        Assert.AreEqual("untouched", reopened["settings:sibling"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_InvalidCandidate_AllowsSubsequentUpdate()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Settings = new { Sibling = "preserved" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        await Assert.ThrowsExactlyAsync<FormatException>(() => configService.UpdateProjectConfigAsync(
            new JsonObject { ["settings:sibling"] = "collision" }).WaitAsync(OperationTimeout));

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["settings"] = new JsonObject { ["next"] = "succeeded" } }).WaitAsync(OperationTimeout);

        Assert.AreEqual("succeeded", configuration["settings:next"]);
        Assert.AreEqual("preserved", configuration["settings:sibling"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_ReloadThrows_AllowsSubsequentUpdate()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Settings = new { Sibling = "preserved" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        using var registration = configuration.GetReloadToken().RegisterChangeCallback(
            _ => throw new InvalidOperationException("Injected reload failure."), null);

        await Assert.ThrowsExactlyAsync<AggregateException>(() => configService.UpdateProjectConfigAsync(
            new JsonObject { ["settings"] = new JsonObject { ["first"] = "committed" } }).WaitAsync(OperationTimeout));

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["settings"] = new JsonObject { ["next"] = "succeeded" } }).WaitAsync(OperationTimeout);

        Assert.AreEqual("committed", configuration["settings:first"]);
        Assert.AreEqual("succeeded", configuration["settings:next"]);
        Assert.AreEqual("preserved", configuration["settings:sibling"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_MixedCaseKeys_UpdatesExistingValueAndReloadsConfiguration()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Paths = new { Source = "original-source", Output = "existing-output" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        Assert.AreEqual("original-source", configuration["paths:source"]);

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["paths"] = new JsonObject { ["source"] = "changed-source" } });

        var config = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(config);
        Assert.HasCount(1, config);
        var paths = config["Paths"]?.AsObject();
        Assert.IsNotNull(paths);
        Assert.HasCount(2, paths);
        Assert.AreEqual("changed-source", paths["Source"]?.GetValue<string>());
        Assert.AreEqual("existing-output", paths["Output"]?.GetValue<string>());
        Assert.AreEqual("changed-source", configuration["paths:source"]);
        Assert.HasCount(1, configuration.AsEnumerable().Where(entry =>
            string.Equals(entry.Key, "paths:source", StringComparison.OrdinalIgnoreCase)));
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("changed-source", reopened["paths:source"]);
        Assert.AreEqual("existing-output", reopened["paths:output"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_MixedCaseNull_RemovesExistingKeyAndPreservesSibling()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Paths = new { Source = "original-source", Output = "existing-output" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["paths"] = new JsonObject { ["source"] = null } });

        var config = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(config);
        Assert.HasCount(1, config);
        var paths = config["Paths"]?.AsObject();
        Assert.IsNotNull(paths);
        Assert.HasCount(1, paths);
        Assert.AreEqual("existing-output", paths["Output"]?.GetValue<string>());
        Assert.IsNull(configuration["paths:source"]);
        Assert.AreEqual("existing-output", configuration["paths:output"]);
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.IsNull(reopened["paths:source"]);
        Assert.AreEqual("existing-output", reopened["paths:output"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task UpdateProjectConfigAsync_Jsonc_PreservesValuesAndKeySpelling(bool emitBom)
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, /*lang=json*/ """
            {
                // Project configuration allows comments and trailing commas.
                "Paths": { "Source": "original-source", "Output": "existing-output", },
                "Labels": { "MixedCaseEntry": "keep-me", },
            }
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["paths"] = new JsonObject { ["source"] = "changed-source" } });

        var config = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(config);
        Assert.AreEqual("changed-source", config["Paths"]?["Source"]?.GetValue<string>());
        Assert.AreEqual("existing-output", config["Paths"]?["Output"]?.GetValue<string>());
        Assert.AreEqual("keep-me", config["Labels"]?["MixedCaseEntry"]?.GetValue<string>());
        Assert.AreEqual("changed-source", configuration["paths:source"]);
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("changed-source", reopened["paths:source"]);
        Assert.AreEqual("keep-me", reopened["labels:mixedcaseentry"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_ArraysAndScalars_ReplacesValuesIncludingNullElements()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Settings = new
            {
                Values = new JsonArray("old", "remove", "remove-again", "remove-last"),
                Scalar = new { Nested = "discarded" },
                Object = "old-scalar",
                Sibling = "preserved"
            }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        await configService.UpdateProjectConfigAsync(new JsonObject
        {
            ["settings"] = new JsonObject
            {
                ["values"] = new JsonArray(null, "replacement", null),
                ["scalar"] = "new-scalar",
                ["object"] = new JsonObject { ["MixedCaseEntry"] = "new-value" }
            }
        });

        var config = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(config);
        var settings = config["Settings"]?.AsObject();
        Assert.IsNotNull(settings);
        var values = settings["Values"]?.AsArray();
        Assert.IsNotNull(values);
        Assert.HasCount(3, values);
        Assert.IsNull(values[0]);
        Assert.AreEqual("replacement", values[1]?.GetValue<string>());
        Assert.IsNull(values[2]);
        Assert.AreEqual("new-scalar", settings["Scalar"]?.GetValue<string>());
        Assert.AreEqual("new-value", settings["Object"]?["MixedCaseEntry"]?.GetValue<string>());
        Assert.AreEqual("preserved", settings["Sibling"]?.GetValue<string>());
        Assert.AreEqual("replacement", configuration["settings:values:1"]);
        Assert.IsNull(configuration["settings:values:3"]);
        Assert.IsNull(configuration["settings:scalar:nested"]);
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.HasCount(3, reopened.GetSection("settings:values").GetChildren());
        Assert.IsNull(reopened["settings:values:0"]);
        Assert.AreEqual("replacement", reopened["settings:values:1"]);
        Assert.IsNull(reopened["settings:values:2"]);
        Assert.AreEqual("new-scalar", reopened["settings:scalar"]);
        Assert.AreEqual("new-value", reopened["settings:object:mixedcaseentry"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_FlattenedCandidateCollision_PreservesBytesAndLiveConfiguration()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Paths = new { Source = "original-source" },
            project = new { name = "Original" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsExactlyAsync<FormatException>(() => configService.UpdateProjectConfigAsync(new JsonObject
        {
            ["project"] = new JsonObject { ["name"] = "Updated" },
            ["paths:source"] = "collision"
        }));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("original-source", configuration["paths:source"]);
        Assert.AreEqual("Original", configuration["project:name"]);
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("original-source", reopened["paths:source"]);
        Assert.AreEqual("Original", reopened["project:name"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_MalformedOriginal_PreservesBytesAndLiveConfiguration()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Paths = new { Source = "original-source" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        await File.WriteAllTextAsync(project.ProjectJsonPath, "{ malformed json");
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsAsync<JsonException>(() => configService.UpdateProjectConfigAsync(
            new JsonObject { ["paths"] = new JsonObject { ["source"] = "changed-source" } }));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("original-source", configuration["paths:source"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ """{"Paths":{"Source":"corrupted"},"paths:source":"collision"}""")]
    [DataRow(/*lang=json,strict*/ """{"Paths":{"Source":"corrupted"},"paths":{"source":"collision"}}""")]
    [DataRow(/*lang=json,strict*/ """{"Paths":{"Source":"corrupted","Source":"duplicate"}}""")]
    [DataRow("""["invalid-root"]""")]
    public async Task UpdateProjectConfigAsync_ProviderInvalidOriginal_PreservesBytesAndLiveConfiguration(string json)
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Paths = new { Source = "original-source" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        await File.WriteAllTextAsync(project.ProjectJsonPath, json);
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);

        await Assert.ThrowsExactlyAsync<FormatException>(() => configService.UpdateProjectConfigAsync(new JsonObject
        {
            ["paths:source"] = null,
            ["paths"] = new JsonObject { ["source"] = "changed-source" }
        }));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("original-source", configuration["paths:source"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_UntouchedCaseSplitObjects_PreservesAllKeys()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Paths = new { Source = "original-source" },
            paths = new { Output = "existing-output" },
            project = new { name = "Original" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["PROJECT"] = new JsonObject { ["NAME"] = "Updated" } });

        var config = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(config);
        Assert.HasCount(3, config);
        Assert.AreEqual("original-source", config["Paths"]?["Source"]?.GetValue<string>());
        Assert.AreEqual("existing-output", config["paths"]?["Output"]?.GetValue<string>());
        Assert.AreEqual("Updated", config["project"]?["name"]?.GetValue<string>());
        Assert.AreEqual("Updated", configuration["project:name"]);
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("original-source", reopened["paths:source"]);
        Assert.AreEqual("existing-output", reopened["paths:output"]);
        Assert.AreEqual("Updated", reopened["project:name"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ """{"project":{"name":"Updated"},"PATHS":{"source":"changed-source"}}""")]
    [DataRow(/*lang=json,strict*/ """{"project":{"name":"Updated"},"PATHS":null}""")]
    [DataRow(/*lang=json,strict*/ """{"project":{"name":"Updated"},"Paths":"replacement"}""")]
    public async Task UpdateProjectConfigAsync_AmbiguousCaseSplitUpdate_PreservesBytesAndLiveConfiguration(string json)
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Paths = new { Source = "original-source" },
            paths = new { Output = "existing-output" },
            project = new { name = "Original" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        var updates = JsonNode.Parse(json)!.AsObject();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => configService.UpdateProjectConfigAsync(updates));

        Assert.IsTrue(exception.Message.Contains("multiple existing keys differ only by case", StringComparison.Ordinal));
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.AreEqual("original-source", configuration["paths:source"]);
        Assert.AreEqual("existing-output", configuration["paths:output"]);
        Assert.AreEqual("Original", configuration["project:name"]);
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.AreEqual("original-source", reopened["paths:source"]);
        Assert.AreEqual("existing-output", reopened["paths:output"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_MissingOriginal_CreatesConfigurationFromUpdates()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            Paths = new { Source = "original-source" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        File.Delete(project.ProjectJsonPath);

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["paths"] = new JsonObject { ["output"] = "new-output" } });

        Assert.IsNull(configuration["paths:source"]);
        Assert.AreEqual("new-output", configuration["paths:output"]);
        using var stream = File.OpenRead(project.ProjectJsonPath);
        using var reopened = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.IsNull(reopened["paths:source"]);
        Assert.AreEqual("new-output", reopened["paths:output"]);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_DeepMergesAndPreservesExistingValues()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            project = new { name = "Original", baseUrl = "https://example.com" },
            theme = new { name = "Lumina" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var updates = new JsonObject
        {
            ["project"] = new JsonObject { ["name"] = "Updated" }
        };

        await configService.UpdateProjectConfigAsync(updates);

        var config = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(config);
        Assert.AreEqual("Updated", config["project"]?["name"]?.GetValue<string>());
        Assert.AreEqual("https://example.com", config["project"]?["baseUrl"]?.GetValue<string>());
        Assert.AreEqual("Lumina", config["theme"]?["name"]?.GetValue<string>());
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_CanceledBeforeReplacement_PreservesOriginalBytes()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            project = new { name = "Original" },
            theme = new { name = "Lumina" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => configService.UpdateProjectConfigAsync(
            new JsonObject { ["theme"] = new JsonObject { ["name"] = "Other" } },
            cancellation.Token));

        var afterCancellation = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        CollectionAssert.AreEqual(original, afterCancellation);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_Success_LeavesNoTempFile()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["theme"] = new JsonObject { ["name"] = "Lumina" } });

        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    [DataRow(UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [DataRow(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead)]
    public async Task UpdateProjectConfigAsync_ExistingUnixPermissions_PreservesMode(UnixFileMode originalMode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file permissions cannot be verified on Windows.");
            return;
        }

        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        File.SetUnixFileMode(project.ProjectJsonPath, originalMode);

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["theme"] = new JsonObject { ["name"] = "Lumina" } });

        Assert.AreEqual(originalMode, File.GetUnixFileMode(project.ProjectJsonPath));
        var config = await configService.ReadProjectConfigAsync();
        Assert.AreEqual("Lumina", config?["theme"]?["name"]?.GetValue<string>());
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_NewUnixFile_CreatesPrivateConfiguration()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file permissions cannot be verified on Windows.");
            return;
        }

        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        File.Delete(project.ProjectJsonPath);

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["theme"] = new JsonObject { ["name"] = "Lumina" } });

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(project.ProjectJsonPath));
        var config = await configService.ReadProjectConfigAsync();
        Assert.AreEqual("Lumina", config?["theme"]?["name"]?.GetValue<string>());
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    private static IReadOnlyList<string> GetTempFiles(string projectPath) =>
        Directory.GetFiles(projectPath, ".project.json.*.tmp");
}
