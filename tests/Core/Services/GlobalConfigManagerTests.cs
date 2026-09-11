using System.Text;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Core.Services;

[TestClass]
[TestCategory("Integration")]
public sealed class GlobalConfigManagerTests
{
    [TestMethod]
    public async Task AddFeedAsync_UnknownSettings_PreservesEntireDocument()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        const string original = """
            {
              "paths": { "source": "photos", "output": "public" },
              "generate": { "images": { "sizes": [320, 640], "enabled": true, "optional": null } },
              "customPlugin": { "rules": [ { "name": "keep", "active": false }, null, [1, 2] ] },
              "theme": { "palette": ["red", "green"], "options": null },
              "packages": { "feeds": { "Existing": "https://example.test/existing" }, "policy": { "trusted": true, "fallbacks": [null, "local"] } },
              "logging": { "logLevel": { "Default": "Debug" }, "sinks": ["file"], "custom": null },
              "defaults": { "theme": "Custom", "future": { "enabled": false } },
              "checkUpdates": false,
              "plugins": { "Existing.Plugin": "1.0.0" },
              "themes": { "Existing.Theme": "2.0.0" }
            }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        await manager.AddFeedAsync("Private", "https://example.test/private");

        var actual = JsonNode.Parse(await File.ReadAllTextAsync(configPath));
        var expected = JsonNode.Parse(original)!;
        expected["packages"]!["feeds"]!["Private"] = "https://example.test/private";
        Assert.AreEqual("https://example.test/private", actual?["packages"]?["feeds"]?["Private"]?.GetValue<string>());
        Assert.IsTrue(JsonNode.DeepEquals(expected, actual), "Adding a feed must retain all unrelated JSON values.");

        var freshManager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);
        await freshManager.AddPluginAsync("New.Plugin", "3.0.0");
        expected["plugins"]!["New.Plugin"] = "3.0.0";
        await AssertDocumentAsync(configPath, expected);

        Assert.IsTrue(await freshManager.RemoveFeedAsync("Private"));
        Assert.IsTrue(expected["packages"]!["feeds"]!.AsObject().Remove("Private"));
        await AssertDocumentAsync(configPath, expected);
        Assert.AreEqual("3.0.0", ReadConfiguration(await File.ReadAllTextAsync(configPath))["plugins:New.Plugin"]);
    }

    [TestMethod]
    [DataRow("packages", "feeds", "plugins", "themes")]
    [DataRow("PACKAGES", "FeEdS", "PLUGINS", "ThEmEs")]
    public async Task ManagedOperations_MixedCaseSections_PreserveSpellingAndSiblings(string packagesName, string feedsName, string pluginsName, string themesName)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        var original = $$"""
            {
              "{{packagesName}}": { "{{feedsName}}": { "Existing": "existing-feed" }, "options": [null, true] },
              "{{pluginsName}}": { "Existing.Plugin": "1.0.0" },
              "{{themesName}}": { "Existing.Theme": "2.0.0" },
              "Logging": { "LogLevel": { "Default": "Trace" }, "future": null },
              "Defaults": { "Theme": "Custom", "future": [false, { "value": 1 }] },
              "CHECKUPDATES": false,
              "custom": { "items": [null, [true, false]] }
            }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var expected = JsonNode.Parse(original)!;
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        await manager.AddFeedAsync("Private", "private-feed");
        await manager.AddPluginAsync("New.Plugin", "3.0.0");
        await manager.AddThemeAsync("New.Theme", "4.0.0");
        await manager.AddPluginAsync("Existing.Plugin", "1.1.0");
        await manager.AddThemeAsync("Existing.Theme", "2.1.0");
        expected[packagesName]![feedsName]!["Private"] = "private-feed";
        expected[pluginsName]!["New.Plugin"] = "3.0.0";
        expected[themesName]!["New.Theme"] = "4.0.0";
        expected[pluginsName]!["Existing.Plugin"] = "1.1.0";
        expected[themesName]!["Existing.Theme"] = "2.1.0";
        await AssertDocumentAsync(configPath, expected);

        var freshManager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);
        var plugins = await freshManager.GetPluginsAsync();
        var themes = await freshManager.GetThemesAsync();
        Assert.HasCount(2, plugins);
        Assert.HasCount(2, themes);
        Assert.AreEqual("1.1.0", plugins["Existing.Plugin"]);
        Assert.AreEqual("2.1.0", themes["Existing.Theme"]);
        Assert.IsFalse(plugins.ContainsKey("existing.plugin"));
        Assert.IsFalse(themes.ContainsKey("existing.theme"));
        Assert.IsFalse(await freshManager.RemoveFeedAsync("existing"));
        Assert.IsFalse(await freshManager.RemovePluginAsync("existing.plugin"));
        Assert.IsFalse(await freshManager.RemoveThemeAsync("existing.theme"));
        Assert.IsTrue(await freshManager.RemoveFeedAsync("Existing"));
        Assert.IsTrue(await freshManager.RemovePluginAsync("Existing.Plugin"));
        Assert.IsTrue(await freshManager.RemoveThemeAsync("Existing.Theme"));
        Assert.IsTrue(expected[packagesName]![feedsName]!.AsObject().Remove("Existing"));
        Assert.IsTrue(expected[pluginsName]!.AsObject().Remove("Existing.Plugin"));
        Assert.IsTrue(expected[themesName]!.AsObject().Remove("Existing.Theme"));
        await AssertDocumentAsync(configPath, expected);
    }

    [TestMethod]
    [DataRow("feeds")]
    [DataRow("Feeds")]
    public async Task ManagedOperations_ProviderValidCaseSplitSections_RetainAllLeaves(string secondFeedsName)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        var original = $$"""
            {
              "packages": { "feeds": { "First": "first-feed" }, "options": { "levels": [null, 1] } },
              "Packages": { "{{secondFeedsName}}": { "Second": "second-feed" }, "options": { "enabled": true } },
              "plugins": { "First.Plugin": "1.0.0" },
              "Plugins": { "Second.Plugin": "2.0.0" },
              "themes": { "First.Theme": "3.0.0" },
              "Themes": { "Second.Theme": "4.0.0" },
              "custom": { "first": [null, false] },
              "Custom": { "second": { "nested": true } },
              "logging": { "logLevel": { "Default": "Debug" } },
              "Logging": { "sink": ["console"] },
              "defaults": { "theme": "Custom" },
              "Defaults": { "future": null }
            }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var expectedValues = ReadConfiguration(original);
        var originalDocument = JsonNode.Parse(original)!;
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        await manager.AddFeedAsync("Private", "private-feed");
        expectedValues["packages:feeds:Private"] = "private-feed";
        var afterFeed = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        foreach (var section in new[] { "plugins", "Plugins", "themes", "Themes", "custom", "Custom", "logging", "Logging", "defaults", "Defaults" })
        {
            Assert.IsTrue(JsonNode.DeepEquals(originalDocument[section], afterFeed[section]), $"Untouched section '{section}' changed.");
        }

        await AssertReaderValuesAsync(configPath, expectedValues);
        var freshManager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);
        Assert.HasCount(2, await freshManager.GetPluginsAsync());
        Assert.HasCount(2, await freshManager.GetThemesAsync());
        await freshManager.AddPluginAsync("New.Plugin", "5.0.0");
        expectedValues["plugins:New.Plugin"] = "5.0.0";
        await freshManager.AddThemeAsync("New.Theme", "6.0.0");
        expectedValues["themes:New.Theme"] = "6.0.0";
        await AssertReaderValuesAsync(configPath, expectedValues);
        Assert.IsTrue(await freshManager.RemoveFeedAsync("Second"));
        Assert.IsTrue(await freshManager.RemovePluginAsync("Second.Plugin"));
        Assert.IsTrue(await freshManager.RemoveThemeAsync("Second.Theme"));
        Assert.IsTrue(expectedValues.Remove("packages:feeds:Second"));
        Assert.IsTrue(expectedValues.Remove("plugins:Second.Plugin"));
        Assert.IsTrue(expectedValues.Remove("themes:Second.Theme"));
        await AssertReaderValuesAsync(configPath, expectedValues);
    }

    [TestMethod]
    [DataRow("{\"paths\": {\"source\": \"photos\"},")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow(/*lang=json,strict*/ "{\"plugins\":{\"Existing\":\"1\"},\"Plugins\":{\"existing\":\"2\"}}")]
    [DataRow(/*lang=json,strict*/ "{\"packages:feeds:Private\":\"1\",\"packages\":{\"feeds\":{\"private\":\"2\"}}}")]
    [DataRow(/*lang=json,strict*/ "{\"unknown\":{\"value\":true,\"VALUE\":false}}")]
    [DataRow(/*lang=json,strict*/ "{\"unknown\":1,\"unknown\":2}")]
    public async Task AddFeedAsync_InvalidOriginal_ThrowsAndRetainsOriginalBytes(string original)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        await File.WriteAllTextAsync(configPath, original);
        var originalBytes = await File.ReadAllBytesAsync(configPath);
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        var error = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => manager.AddFeedAsync("Private", "private-feed"));

        Assert.IsNotEmpty(error.Message);
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => manager.AddPluginAsync("New.Plugin", "1.0.0"));
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ "{\"packages\":null}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"packages\":[]}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"packages\":\"invalid\"}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"packages\":{\"feeds\":null}}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"packages\":{\"feeds\":false}}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"packages\":{\"feeds\":{\"Existing\":true}}}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"plugins\":null}", "plugin")]
    [DataRow(/*lang=json,strict*/ "{\"plugins\":[]}", "plugin")]
    [DataRow(/*lang=json,strict*/ "{\"themes\":null}", "theme")]
    [DataRow(/*lang=json,strict*/ "{\"themes\":{\"Existing\":{\"invalid\":true}}}", "theme")]
    public async Task ManagedOperations_InvalidSectionType_FailUnchanged(string original, string operation)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        await File.WriteAllTextAsync(configPath, original);
        var originalBytes = await File.ReadAllBytesAsync(configPath);
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => AddEntryAsync(manager, operation, "New"));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => RemoveEntryAsync(manager, operation, "Existing"));

        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));
    }

    [TestMethod]
    [DataRow("feed", "existing")]
    [DataRow("feed", "ColonFeed")]
    [DataRow("plugin", "original.plugin")]
    [DataRow("plugin", "Colon.Plugin")]
    [DataRow("theme", "original.theme")]
    [DataRow("theme", "Colon.Theme")]
    public async Task ManagedOperations_ReaderRejectsCandidate_PreserveFileAndCachedDocument(string operation, string name)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        const string original = """
            {
              "packages": { "feeds": { "Existing": "existing-feed" } },
              "plugins": { "Original.Plugin": "1.0.0" },
              "themes": { "Original.Theme": "2.0.0" },
              "packages:feeds:ColonFeed": "flat-feed",
              "plugins:Colon.Plugin": "3.0.0",
              "themes:Colon.Theme": "4.0.0",
              "custom": [null, { "enabled": false }]
            }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var originalBytes = await File.ReadAllBytesAsync(configPath);
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        var error = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => AddEntryAsync(manager, operation, name));

        Assert.IsNotEmpty(error.Message);
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));
        await manager.AddPluginAsync("Valid.Plugin", "5.0.0");
        var expected = JsonNode.Parse(original)!;
        expected["plugins"]!["Valid.Plugin"] = "5.0.0";
        await AssertDocumentAsync(configPath, expected);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, "test-global.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ManagedOperations_CanceledToken_LeaveOriginalAndCacheUnchanged(bool warmCache)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        const string original = """
            { "packages": { "feeds": { "Existing": "existing-feed" } },
              "plugins": { "Existing.Plugin": "1.0.0" }, "themes": { "Existing.Theme": "2.0.0" },
              "custom": [null, false, { "enabled": true }] }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var originalBytes = await File.ReadAllBytesAsync(configPath);
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);
        if (warmCache)
        {
            Assert.HasCount(1, await manager.GetPluginsAsync());
        }

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.AddFeedAsync("Canceled", "canceled-feed", cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.AddPluginAsync("Canceled.Plugin", "3.0.0", cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.AddThemeAsync("Canceled.Theme", "4.0.0", cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.RemoveFeedAsync("Existing", cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.RemovePluginAsync("Existing.Plugin", cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.RemoveThemeAsync("Existing.Theme", cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.GetPluginsAsync(cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.GetThemesAsync(cancellation.Token));
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));

        await manager.AddFeedAsync("Valid", "valid-feed");
        var expected = JsonNode.Parse(original)!;
        expected["packages"]!["feeds"]!["Valid"] = "valid-feed";
        await AssertDocumentAsync(configPath, expected);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, "test-global.json.*.tmp"));
    }

    [TestMethod]
    public async Task GetPluginsAsync_CanceledMissingFile_DoesNotCreateDefaults()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.GetPluginsAsync(cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.AddFeedAsync("Private", "private-feed", cancellation.Token));

        Assert.IsFalse(File.Exists(configPath));
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, "test-global.json.*.tmp"));
    }

    [TestMethod]
    public async Task GetThemesAsync_MissingFile_CreatesExistingDefaultsAtExplicitPath()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);
        Assert.AreEqual(configPath, manager.ConfigFilePath);
        Assert.IsFalse(manager.ConfigFileExists());

        var themes = await manager.GetThemesAsync();

        Assert.IsEmpty(themes);
        Assert.IsTrue(manager.ConfigFileExists());
        var expected = JsonNode.Parse("""
            {
              "packages": { "feeds": {} },
              "logging": { "logLevel": { "Default": "Warning", "Spectara.Revela": "Warning", "Microsoft": "Warning", "System": "Warning" } },
              "defaults": { "theme": "Lumina" },
              "checkUpdates": true,
              "themes": {},
              "plugins": {}
            }
            """)!;
        await AssertDocumentAsync(configPath, expected);
        if (!OperatingSystem.IsWindows())
        {
            Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(configPath));
        }
    }

    [TestMethod]
    [DataRow("nuget.org")]
    [DataRow("NuGet.OrG")]
    public async Task FeedOperations_ReservedName_RejectWithoutWriting(string name)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        const string original = /*lang=json,strict*/ "{ \"packages\": { \"feeds\": {} }, \"custom\": [null, true] }";
        await File.WriteAllTextAsync(configPath, original);
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.AddFeedAsync(name, "private-feed"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.RemoveFeedAsync(name));

        Assert.AreEqual(original, await File.ReadAllTextAsync(configPath));
    }

    [TestMethod]
    public async Task ManagedOperations_MissingEntriesAndDuplicateFeed_DoNotWrite()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        const string original = /*lang=json,strict*/ "{ \"packages\": { \"feeds\": { \"Existing\": \"existing-feed\" } }, \"custom\": null }";
        await File.WriteAllTextAsync(configPath, original);
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.AddFeedAsync("Existing", "replacement-feed"));
        Assert.IsFalse(await manager.RemoveFeedAsync("Missing"));
        Assert.IsFalse(await manager.RemovePluginAsync("Missing.Plugin"));
        Assert.IsFalse(await manager.RemoveThemeAsync("Missing.Theme"));
        Assert.IsEmpty(await manager.GetPluginsAsync());
        Assert.IsEmpty(await manager.GetThemesAsync());

        Assert.AreEqual(original, await File.ReadAllTextAsync(configPath));
    }

    [TestMethod]
    public async Task AddPluginAsync_JsonCommentsAndNullMapping_PreservesValues()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        await File.WriteAllTextAsync(configPath, /*lang=json*/ """
            {
              // Global configuration permits comments and trailing commas.
              "plugins": { "Existing.Plugin": null, },
              "custom": [null, true,],
            }
            """);
        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        await manager.AddPluginAsync("New.Plugin", "1.0.0");

        var expected = JsonNode.Parse("""
            { "plugins": { "Existing.Plugin": null, "New.Plugin": "1.0.0" }, "custom": [null, true] }
            """)!;
        await AssertDocumentAsync(configPath, expected);
        var plugins = await manager.GetPluginsAsync();
        Assert.HasCount(2, plugins);
        Assert.IsNull(plugins["Existing.Plugin"]);
        Assert.AreEqual("1.0.0", plugins["New.Plugin"]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddFeedAsync_UnixFile_CreatesPrivateConfiguration(bool replaceExisting)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file permissions cannot be verified on Windows.");
            return;
        }

        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        if (replaceExisting)
        {
            await File.WriteAllTextAsync(configPath, "{}");
            File.SetUnixFileMode(configPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        var manager = new GlobalConfigManager(NullLogger<GlobalConfigManager>.Instance, configPath);

        await manager.AddFeedAsync("Private", "private-feed");

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(configPath));
        Assert.AreEqual("private-feed", ReadConfiguration(await File.ReadAllTextAsync(configPath))["packages:feeds:Private"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, "test-global.json.*.tmp"));
    }

    private static Task AddEntryAsync(GlobalConfigManager manager, string operation, string name) => operation switch
    {
        "feed" => manager.AddFeedAsync(name, "new-feed"),
        "plugin" => manager.AddPluginAsync(name, "9.0.0"),
        "theme" => manager.AddThemeAsync(name, "9.0.0"),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static Task<bool> RemoveEntryAsync(GlobalConfigManager manager, string operation, string name) => operation switch
    {
        "feed" => manager.RemoveFeedAsync(name),
        "plugin" => manager.RemovePluginAsync(name),
        "theme" => manager.RemoveThemeAsync(name),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static Dictionary<string, string?> ReadConfiguration(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var reader = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        return reader.AsEnumerable().ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task AssertDocumentAsync(string configPath, JsonNode expected)
    {
        var json = await File.ReadAllTextAsync(configPath);
        var actual = JsonNode.Parse(json);
        Assert.IsTrue(JsonNode.DeepEquals(expected, actual), "The write must apply only the expected JSON changes.");
        await AssertReaderValuesAsync(configPath, ReadConfiguration(expected.ToJsonString()));
    }

    private static async Task AssertReaderValuesAsync(string configPath, IReadOnlyDictionary<string, string?> expected)
    {
        var actual = ReadConfiguration(await File.ReadAllTextAsync(configPath));
        Assert.HasCount(expected.Count, actual);
        foreach (var entry in expected)
        {
            Assert.IsTrue(actual.TryGetValue(entry.Key, out var value), $"Missing configuration key '{entry.Key}'.");
            Assert.AreEqual(entry.Value, value, $"Configuration key '{entry.Key}' changed.");
        }
    }
}
