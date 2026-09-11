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
