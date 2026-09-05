using Microsoft.Extensions.Logging.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Core.Services;

/// <summary>
/// Unit tests for <see cref="AssetResolver"/> page-type-scoped stylesheet loading.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class AssetResolverScopeTests
{
    private string tempProject = null!;

    [TestInitialize]
    public void Setup()
    {
        // Empty project dir so ScanLocalOverrides is a no-op.
        tempProject = Path.Combine(Path.GetTempPath(), $"AssetResolverScopeTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempProject);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempProject))
        {
            Directory.Delete(tempProject, recursive: true);
        }
    }

    private static AssetResolver CreateResolver() => new(NullLogger<AssetResolver>.Instance);

    private static FakeTheme BaseTheme() => new(
        name: "Lumina",
        prefix: null,
        targetTheme: null,
        files: ["Assets/main.css", "Assets/photo.css", "Assets/lightbox.js"],
        stylesheets:
        [
            new AssetDeclaration { Path = "main.css" },                       // no scope => all
            new AssetDeclaration { Path = "photo.css", Scope = ["photo"] }
        ],
        scripts: [new AssetDeclaration { Path = "lightbox.js", Scope = ["lightbox"] }]);

    private static FakeTheme StatisticsExtension() => new(
        name: "Lumina Statistics",
        prefix: "statistics",
        targetTheme: "Lumina",
        files: ["Assets/main.css", "Assets/main.js"],
        stylesheets: [new AssetDeclaration { Path = "main.css", Scope = ["statistics"] }],
        scripts: [new AssetDeclaration { Path = "main.js", Scope = ["statistics"] }]);

    [TestMethod]
    public void PhotoScope_IncludesMainAndPhoto_ExcludesStatistics()
    {
        var resolver = CreateResolver();
        resolver.Initialize(BaseTheme(), [StatisticsExtension()], tempProject);

        var css = resolver.GetStyleSheets("photo");

        CollectionAssert.Contains(css.ToList(), "main.css");
        CollectionAssert.Contains(css.ToList(), "photo.css");
        CollectionAssert.DoesNotContain(css.ToList(), "statistics/main.css");
        CollectionAssert.DoesNotContain(resolver.GetScripts("photo").ToList(), "statistics/main.js");
    }

    [TestMethod]
    public void StatisticsScope_IncludesMainAndStatistics_ExcludesPhoto()
    {
        var resolver = CreateResolver();
        resolver.Initialize(BaseTheme(), [StatisticsExtension()], tempProject);

        var css = resolver.GetStyleSheets("statistics");

        CollectionAssert.Contains(css.ToList(), "main.css");
        CollectionAssert.Contains(css.ToList(), "statistics/main.css");
        CollectionAssert.DoesNotContain(css.ToList(), "photo.css");
        CollectionAssert.Contains(resolver.GetScripts("statistics").ToList(), "statistics/main.js");
    }

    [TestMethod]
    public void IndexScope_IncludesOnlyUnscopedMain()
    {
        var resolver = CreateResolver();
        resolver.Initialize(BaseTheme(), [StatisticsExtension()], tempProject);

        var css = resolver.GetStyleSheets("index");

        CollectionAssert.Contains(css.ToList(), "main.css");
        CollectionAssert.DoesNotContain(css.ToList(), "photo.css");
        CollectionAssert.DoesNotContain(css.ToList(), "statistics/main.css");
    }

    [TestMethod]
    public void NullScope_IncludesEverything()
    {
        var resolver = CreateResolver();
        resolver.Initialize(BaseTheme(), [StatisticsExtension()], tempProject);

        var css = resolver.GetStyleSheets(null);

        CollectionAssert.Contains(css.ToList(), "main.css");
        CollectionAssert.Contains(css.ToList(), "photo.css");
        CollectionAssert.Contains(css.ToList(), "statistics/main.css");
    }

    [TestMethod]
    public void UndeclaredAssets_AreNotLinkedOnScopedPages()
    {
        var legacyExtension = new FakeTheme(
            name: "Legacy",
            prefix: "legacy",
            targetTheme: "Lumina",
            files: ["Assets/legacy.css", "Assets/legacy.js"],
            stylesheets: null,
            scripts: null);

        var resolver = CreateResolver();
        resolver.Initialize(BaseTheme(), [legacyExtension], tempProject);

        CollectionAssert.DoesNotContain(resolver.GetStyleSheets("photo").ToList(), "legacy/legacy.css");
        CollectionAssert.DoesNotContain(resolver.GetStyleSheets("index").ToList(), "legacy/legacy.css");
        CollectionAssert.DoesNotContain(resolver.GetScripts("photo").ToList(), "legacy/legacy.js");
        CollectionAssert.DoesNotContain(resolver.GetScripts("index").ToList(), "legacy/legacy.js");
    }

    [TestMethod]
    public void ProjectAssetScopes_ApplyToNewLocalAssets()
    {
        var localAssets = Path.Combine(tempProject, ProjectPaths.Themes, "Lumina", "Assets");
        Directory.CreateDirectory(localAssets);
        File.WriteAllText(Path.Combine(localAssets, "prism.css"), "code {}");
        File.WriteAllText(Path.Combine(localAssets, "prism.js"), "Prism.highlightAll();");
        File.WriteAllText(Path.Combine(localAssets, "website.css"), "body {}");
        File.WriteAllText(Path.Combine(localAssets, "website.js"), "console.log('website');");
        File.WriteAllText(
                Path.Combine(tempProject, "site.json"),
                              /*lang=json*/
                              """
                        {
                            // Strings remain global for backward compatibility.
                            "stylesheets": [
                                "website.css",
                                {
                                    "path": "prism.css",
                                    "scope": ["docs",],
                                },
                            ],
                            "scripts": [
                                "website.js",
                                {
                                    "path": "prism.js",
                                    "scope": ["docs",],
                                },
                            ],
                        }
                        """);
        var resolver = CreateResolver();
        resolver.Initialize(BaseTheme(), [], tempProject);

        CollectionAssert.Contains(resolver.GetStyleSheets("docs").ToList(), "prism.css");
        CollectionAssert.DoesNotContain(resolver.GetStyleSheets("index").ToList(), "prism.css");
        CollectionAssert.DoesNotContain(resolver.GetStyleSheets("gallery").ToList(), "prism.css");
        CollectionAssert.DoesNotContain(resolver.GetStyleSheets("photo").ToList(), "prism.css");
        CollectionAssert.Contains(resolver.GetStyleSheets("photo").ToList(), "website.css");
        CollectionAssert.Contains(resolver.GetScripts("docs").ToList(), "prism.js");
        CollectionAssert.DoesNotContain(resolver.GetScripts("index").ToList(), "prism.js");
        CollectionAssert.DoesNotContain(resolver.GetScripts("gallery").ToList(), "prism.js");
        CollectionAssert.DoesNotContain(resolver.GetScripts("photo").ToList(), "prism.js");
        CollectionAssert.Contains(resolver.GetScripts("photo").ToList(), "website.js");
        CollectionAssert.DoesNotContain(resolver.GetScripts("photo").ToList(), "lightbox.js");
        CollectionAssert.Contains(resolver.GetScripts("lightbox").ToList(), "lightbox.js");
    }

    private sealed class FakeTheme(
        string name,
        string? prefix,
        string? targetTheme,
        IReadOnlyList<string> files,
        IReadOnlyList<AssetDeclaration>? stylesheets,
        IReadOnlyList<AssetDeclaration>? scripts) : ITheme
    {
        private readonly IReadOnlyList<string> files = files;

        public PackageMetadata Metadata { get; } = new()
        {
            Id = $"Spectara.Revela.Themes.{name}",
            Name = name,
            Version = "1.0.0",
            Description = string.Empty,
            Author = "Test"
        };

        public string? Prefix { get; } = prefix;
        public string? TargetTheme { get; } = targetTheme;
        public ThemeManifest Manifest { get; } = new()
        {
            LayoutTemplate = "Layout.revela",
            Stylesheets = stylesheets,
            Scripts = scripts
        };

        public Stream? GetFile(string relativePath) =>
            files.Contains(relativePath) ? new MemoryStream([1, 2, 3]) : null;

        public IEnumerable<string> GetAllFiles() => files;

        public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
