using System.Text;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Spectara.Revela.Core.Configuration;
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
              "dependencies": { "feeds": { "Existing": "https://example.test/existing" }, "packages": { "Existing.Plugin": "1.0.0" }, "policy": { "trusted": true, "fallbacks": [null, "local"] } },
              "logging": { "logLevel": { "Default": "Debug" }, "sinks": ["file"], "custom": null },
              "defaults": { "theme": "Custom", "future": { "enabled": false } },
              "checkUpdates": false,
              "plugins": { "serve": { "port": 8080 } }
            }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var manager = CreateManager(configPath);

        await manager.AddFeedAsync("Private", "https://example.test/private");

        var actual = JsonNode.Parse(await File.ReadAllTextAsync(configPath));
        var expected = JsonNode.Parse(original)!;
        expected["dependencies"]!["feeds"]!["Private"] = "https://example.test/private";
        Assert.AreEqual("https://example.test/private", actual?["dependencies"]?["feeds"]?["Private"]?.GetValue<string>());
        Assert.IsTrue(JsonNode.DeepEquals(expected, actual), "Adding a feed must retain all unrelated JSON values.");

        var freshManager = CreateManager(configPath);
        await freshManager.AddPackageAsync("New.Plugin", "3.0.0");
        expected["dependencies"]!["packages"]!["New.Plugin"] = "3.0.0";
        await AssertDocumentAsync(configPath, expected);

        Assert.IsTrue(await freshManager.RemoveFeedAsync("Private"));
        Assert.IsTrue(expected["dependencies"]!["feeds"]!.AsObject().Remove("Private"));
        await AssertDocumentAsync(configPath, expected);
        var values = ReadConfiguration(await File.ReadAllTextAsync(configPath));
        Assert.AreEqual("3.0.0", values["dependencies:packages:New.Plugin"]);
        Assert.AreEqual("8080", values["plugins:serve:port"], "The root plugins node holds plugin settings and must stay untouched.");
    }

    [TestMethod]
    public async Task AddPackageAsync_MissingDependenciesSection_WritesExactVersionIntoNewShape()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        await File.WriteAllTextAsync(configPath, /*lang=json,strict*/ "{ \"theme\": { \"name\": \"Lumina\" } }");
        var manager = CreateManager(configPath);

        await manager.AddPackageAsync("Acme.Revela.Watermark", "1.2.3-beta.4");

        var expected = JsonNode.Parse("""
            { "theme": { "name": "Lumina" }, "dependencies": { "packages": { "Acme.Revela.Watermark": "1.2.3-beta.4" } } }
            """)!;
        await AssertDocumentAsync(configPath, expected);
        var packages = await CreateManager(configPath).GetPackagesAsync();
        Assert.HasCount(1, packages);
        Assert.AreEqual("1.2.3-beta.4", packages["Acme.Revela.Watermark"]);
    }

    [TestMethod]
    [DataRow("dependencies", "feeds", "packages")]
    [DataRow("DEPENDENCIES", "FeEdS", "PaCkAgEs")]
    public async Task ManagedOperations_MixedCaseSections_PreserveSpellingAndSiblings(string dependenciesName, string feedsName, string packagesName)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        var original = $$"""
            {
              "{{dependenciesName}}": {
                "{{feedsName}}": { "Existing": "existing-feed" },
                "{{packagesName}}": { "Existing.Plugin": "1.0.0", "Existing.Theme": "2.0.0" },
                "options": [null, true]
              },
              "Logging": { "LogLevel": { "Default": "Trace" }, "future": null },
              "Defaults": { "Theme": "Custom", "future": [false, { "value": 1 }] },
              "CHECKUPDATES": false,
              "custom": { "items": [null, [true, false]] }
            }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var expected = JsonNode.Parse(original)!;
        var manager = CreateManager(configPath);

        await manager.AddFeedAsync("Private", "private-feed");
        await manager.AddPackageAsync("New.Plugin", "3.0.0");
        await manager.AddPackageAsync("Existing.Plugin", "1.1.0");
        expected[dependenciesName]![feedsName]!["Private"] = "private-feed";
        expected[dependenciesName]![packagesName]!["New.Plugin"] = "3.0.0";
        expected[dependenciesName]![packagesName]!["Existing.Plugin"] = "1.1.0";
        await AssertDocumentAsync(configPath, expected);

        var freshManager = CreateManager(configPath);
        var packages = await freshManager.GetPackagesAsync();
        Assert.HasCount(3, packages);
        Assert.AreEqual("1.1.0", packages["Existing.Plugin"]);
        Assert.AreEqual("2.0.0", packages["Existing.Theme"]);
        Assert.IsFalse(packages.ContainsKey("existing.plugin"));
        Assert.IsFalse(await freshManager.RemoveFeedAsync("existing"));
        Assert.IsFalse(await freshManager.RemovePackageAsync("existing.plugin"));
        Assert.IsTrue(await freshManager.RemoveFeedAsync("Existing"));
        Assert.IsTrue(await freshManager.RemovePackageAsync("Existing.Plugin"));
        Assert.IsTrue(expected[dependenciesName]![feedsName]!.AsObject().Remove("Existing"));
        Assert.IsTrue(expected[dependenciesName]![packagesName]!.AsObject().Remove("Existing.Plugin"));
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
              "dependencies": { "feeds": { "First": "first-feed" }, "packages": { "First.Plugin": "1.0.0" }, "options": { "levels": [null, 1] } },
              "Dependencies": { "{{secondFeedsName}}": { "Second": "second-feed" }, "Packages": { "Second.Plugin": "2.0.0" }, "options": { "enabled": true } },
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
        var manager = CreateManager(configPath);

        await manager.AddFeedAsync("Private", "private-feed");
        expectedValues["dependencies:feeds:Private"] = "private-feed";
        var afterFeed = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        foreach (var section in new[] { "custom", "Custom", "logging", "Logging", "defaults", "Defaults" })
        {
            Assert.IsTrue(JsonNode.DeepEquals(originalDocument[section], afterFeed[section]), $"Untouched section '{section}' changed.");
        }

        await AssertReaderValuesAsync(configPath, expectedValues);
        var freshManager = CreateManager(configPath);
        Assert.HasCount(2, await freshManager.GetPackagesAsync());
        await freshManager.AddPackageAsync("New.Plugin", "5.0.0");
        expectedValues["dependencies:packages:New.Plugin"] = "5.0.0";
        await AssertReaderValuesAsync(configPath, expectedValues);
        Assert.IsTrue(await freshManager.RemoveFeedAsync("Second"));
        Assert.IsTrue(await freshManager.RemovePackageAsync("Second.Plugin"));
        Assert.IsTrue(expectedValues.Remove("dependencies:feeds:Second"));
        Assert.IsTrue(expectedValues.Remove("dependencies:packages:Second.Plugin"));
        await AssertReaderValuesAsync(configPath, expectedValues);
    }

    [TestMethod]
    [DataRow("{\"paths\": {\"source\": \"photos\"},")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":{\"packages\":{\"Existing\":\"1\"}},\"Dependencies\":{\"Packages\":{\"existing\":\"2\"}}}")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies:feeds:Private\":\"1\",\"dependencies\":{\"feeds\":{\"private\":\"2\"}}}")]
    [DataRow(/*lang=json,strict*/ "{\"unknown\":{\"value\":true,\"VALUE\":false}}")]
    [DataRow(/*lang=json,strict*/ "{\"unknown\":1,\"unknown\":2}")]
    public async Task AddFeedAsync_InvalidOriginal_ThrowsAndRetainsOriginalBytes(string original)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        await File.WriteAllTextAsync(configPath, original);
        var originalBytes = await File.ReadAllBytesAsync(configPath);
        var manager = CreateManager(configPath);

        var error = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => manager.AddFeedAsync("Private", "private-feed"));

        Assert.IsNotEmpty(error.Message);
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => manager.AddPackageAsync("New.Plugin", "1.0.0"));
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":null}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":[]}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":\"invalid\"}", "package")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":{\"feeds\":null}}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":{\"feeds\":false}}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":{\"feeds\":{\"Existing\":true}}}", "feed")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":{\"packages\":null}}", "package")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":{\"packages\":[]}}", "package")]
    [DataRow(/*lang=json,strict*/ "{\"dependencies\":{\"packages\":{\"Existing\":{\"invalid\":true}}}}", "package")]
    public async Task ManagedOperations_InvalidSectionType_FailUnchanged(string original, string operation)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        await File.WriteAllTextAsync(configPath, original);
        var originalBytes = await File.ReadAllBytesAsync(configPath);
        var manager = CreateManager(configPath);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => AddEntryAsync(manager, operation, "New"));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => RemoveEntryAsync(manager, operation, "Existing"));

        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));
    }

    [TestMethod]
    [DataRow("feed", "existing")]
    [DataRow("feed", "ColonFeed")]
    [DataRow("package", "original.plugin")]
    [DataRow("package", "Colon.Plugin")]
    public async Task ManagedOperations_ReaderRejectsCandidate_PreserveFileAndCachedDocument(string operation, string name)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        const string original = """
            {
              "dependencies": { "feeds": { "Existing": "existing-feed" }, "packages": { "Original.Plugin": "1.0.0" } },
              "dependencies:feeds:ColonFeed": "flat-feed",
              "dependencies:packages:Colon.Plugin": "3.0.0",
              "custom": [null, { "enabled": false }]
            }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var originalBytes = await File.ReadAllBytesAsync(configPath);
        var manager = CreateManager(configPath);

        var error = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => AddEntryAsync(manager, operation, name));

        Assert.IsNotEmpty(error.Message);
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));
        await manager.AddPackageAsync("Valid.Plugin", "5.0.0");
        var expected = JsonNode.Parse(original)!;
        expected["dependencies"]!["packages"]!["Valid.Plugin"] = "5.0.0";
        await AssertDocumentAsync(configPath, expected);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".test-global.json.*.tmp"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ManagedOperations_CanceledToken_LeaveOriginalAndCacheUnchanged(bool warmCache)
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        const string original = """
            { "dependencies": { "feeds": { "Existing": "existing-feed" }, "packages": { "Existing.Plugin": "1.0.0" } },
              "custom": [null, false, { "enabled": true }] }
            """;
        await File.WriteAllTextAsync(configPath, original);
        var originalBytes = await File.ReadAllBytesAsync(configPath);
        var manager = CreateManager(configPath);
        if (warmCache)
        {
            Assert.HasCount(1, await manager.GetPackagesAsync());
        }

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manager.AddFeedAsync("Canceled", "canceled-feed", cancellation.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manager.AddPackageAsync("Canceled.Plugin", "3.0.0", cancellation.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manager.RemoveFeedAsync("Existing", cancellation.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manager.RemovePackageAsync("Existing.Plugin", cancellation.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manager.GetPackagesAsync(cancellation.Token));
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(configPath));

        await manager.AddFeedAsync("Valid", "valid-feed");
        var expected = JsonNode.Parse(original)!;
        expected["dependencies"]!["feeds"]!["Valid"] = "valid-feed";
        await AssertDocumentAsync(configPath, expected);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".test-global.json.*.tmp"));
    }

    [TestMethod]
    public async Task GetPackagesAsync_CanceledMissingFile_DoesNotCreateDefaults()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        var manager = CreateManager(configPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manager.GetPackagesAsync(cancellation.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manager.AddFeedAsync("Private", "private-feed", cancellation.Token));

        Assert.IsFalse(File.Exists(configPath));
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".test-global.json.*.tmp"));
    }

    [TestMethod]
    public async Task GetPackagesAsync_MissingFile_ReturnsEmptyWithoutCreatingFile()
    {
        // First-run detection relies on revela.json not existing until something is saved.
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        var manager = CreateManager(configPath);
        Assert.AreEqual(configPath, manager.ConfigFilePath);

        var packages = await manager.GetPackagesAsync();

        Assert.IsEmpty(packages);
        Assert.IsFalse(manager.ConfigFileExists());
        Assert.IsFalse(await manager.RemoveFeedAsync("Missing"));
        Assert.IsFalse(manager.ConfigFileExists());
    }

    [TestMethod]
    public async Task AddFeedAsync_MissingFile_CreatesFileWithOnlyTheFeed()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        var manager = CreateManager(configPath);

        await manager.AddFeedAsync("Private", "https://example.test/private");

        var expected = JsonNode.Parse("""
            { "dependencies": { "feeds": { "Private": "https://example.test/private" } } }
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
        const string original = /*lang=json,strict*/ "{ \"dependencies\": { \"feeds\": {} }, \"custom\": [null, true] }";
        await File.WriteAllTextAsync(configPath, original);
        var manager = CreateManager(configPath);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.AddFeedAsync(name, "private-feed"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.RemoveFeedAsync(name));

        Assert.AreEqual(original, await File.ReadAllTextAsync(configPath));
    }

    [TestMethod]
    public async Task ManagedOperations_MissingEntriesAndDuplicateFeed_DoNotWrite()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        const string original = /*lang=json,strict*/ "{ \"dependencies\": { \"feeds\": { \"Existing\": \"existing-feed\" } }, \"custom\": null }";
        await File.WriteAllTextAsync(configPath, original);
        var manager = CreateManager(configPath);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.AddFeedAsync("Existing", "replacement-feed"));
        Assert.IsFalse(await manager.RemoveFeedAsync("Missing"));
        Assert.IsFalse(await manager.RemovePackageAsync("Missing.Plugin"));
        Assert.IsEmpty(await manager.GetPackagesAsync());

        Assert.AreEqual(original, await File.ReadAllTextAsync(configPath));
    }

    [TestMethod]
    public async Task AddPackageAsync_JsonCommentsAndNullMapping_PreservesValues()
    {
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        await File.WriteAllTextAsync(configPath, /*lang=json*/ """
            {
              // Global configuration permits comments and trailing commas.
              "dependencies": { "packages": { "Existing.Plugin": null, }, },
              "custom": [null, true,],
            }
            """);
        var manager = CreateManager(configPath);

        await manager.AddPackageAsync("New.Plugin", "1.0.0");

        var expected = JsonNode.Parse("""
            { "dependencies": { "packages": { "Existing.Plugin": null, "New.Plugin": "1.0.0" } }, "custom": [null, true] }
            """)!;
        await AssertDocumentAsync(configPath, expected);
        var packages = await manager.GetPackagesAsync();
        Assert.HasCount(2, packages);
        Assert.IsNull(packages["Existing.Plugin"]);
        Assert.AreEqual("1.0.0", packages["New.Plugin"]);
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

        var manager = CreateManager(configPath);

        await manager.AddFeedAsync("Private", "private-feed");

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(configPath));
        Assert.AreEqual("private-feed", ReadConfiguration(await File.ReadAllTextAsync(configPath))["dependencies:feeds:Private"]);
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".test-global.json.*.tmp"));
    }

    [TestMethod]
    [DataRow("feed")]
    [DataRow("package")]
    public async Task ManagedOperations_Write_ReloadsConfiguration(string operation)
    {
        // The interactive menu runs `config feed add` and then install in one process:
        // the install must see the new feed without a restart.
        using var project = TestProject.Create();
        var configPath = Path.Combine(project.RootPath, "test-global.json");
        await File.WriteAllTextAsync(configPath, "{}");
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
        var manager = CreateManager(configPath, configuration);

        await AddEntryAsync(manager, operation, "Added");

        var key = operation == "feed" ? "dependencies:feeds:Added" : "dependencies:packages:Added";
        Assert.IsNotNull(configuration[key]);
    }

    private static GlobalConfigManager CreateManager(string configPath, IConfiguration? configuration = null) =>
        new(
            NullLogger<GlobalConfigManager>.Instance,
            new ConfigFileWriter(configuration ?? new ConfigurationBuilder().Build(), NullLogger<ConfigFileWriter>.Instance),
            configPath);

    private static Task AddEntryAsync(GlobalConfigManager manager, string operation, string name) => operation switch
    {
        "feed" => manager.AddFeedAsync(name, "new-feed"),
        "package" => manager.AddPackageAsync(name, "9.0.0"),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static Task<bool> RemoveEntryAsync(GlobalConfigManager manager, string operation, string name) => operation switch
    {
        "feed" => manager.RemoveFeedAsync(name),
        "package" => manager.RemovePackageAsync(name),
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
