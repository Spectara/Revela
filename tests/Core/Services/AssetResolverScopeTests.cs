using Microsoft.Extensions.Logging.Abstractions;
using Spectara.Revela.Core.Services;
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
        files: ["Assets/main.css", "Assets/photo.css"],
        stylesheets:
        [
            new StylesheetDeclaration { Path = "main.css" },                       // no scope => all
            new StylesheetDeclaration { Path = "photo.css", Scope = ["photo"] }
        ]);

    private static FakeTheme StatisticsExtension() => new(
        name: "Lumina Statistics",
        prefix: "statistics",
        targetTheme: "Lumina",
        files: ["Assets/main.css"],
        stylesheets: [new StylesheetDeclaration { Path = "main.css", Scope = ["statistics"] }]);

    [TestMethod]
    public void PhotoScope_IncludesMainAndPhoto_ExcludesStatistics()
    {
        var resolver = CreateResolver();
        resolver.Initialize(BaseTheme(), [StatisticsExtension()], tempProject);

        var css = resolver.GetStyleSheets("photo");

        CollectionAssert.Contains(css.ToList(), "main.css");
        CollectionAssert.Contains(css.ToList(), "photo.css");
        CollectionAssert.DoesNotContain(css.ToList(), "statistics/main.css");
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
    public void UndeclaredStylesheet_LoadsOnEveryScope()
    {
        // Extension with a CSS file but NO stylesheet declarations => backward-compatible "load everywhere".
        var legacyExtension = new FakeTheme(
            name: "Legacy",
            prefix: "legacy",
            targetTheme: "Lumina",
            files: ["Assets/legacy.css"],
            stylesheets: null);

        var resolver = CreateResolver();
        resolver.Initialize(BaseTheme(), [legacyExtension], tempProject);

        CollectionAssert.Contains(resolver.GetStyleSheets("photo").ToList(), "legacy/legacy.css");
        CollectionAssert.Contains(resolver.GetStyleSheets("index").ToList(), "legacy/legacy.css");
    }

    private sealed class FakeTheme(
        string name,
        string? prefix,
        string? targetTheme,
        IReadOnlyList<string> files,
        IReadOnlyList<StylesheetDeclaration>? stylesheets) : ITheme
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
            Stylesheets = stylesheets
        };

        public Stream? GetFile(string relativePath) =>
            files.Contains(relativePath) ? new MemoryStream([1, 2, 3]) : null;

        public IEnumerable<string> GetAllFiles() => files;

        public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
