using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Core.Services;

/// <summary>
/// Unit tests for <see cref="AssetResolver.GetFingerprint"/>, the content hash behind
/// cache-busting <c>asset_url</c> query strings.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class AssetResolverFingerprintTests
{
    private string tempProject = null!;

    [TestInitialize]
    public void Setup()
    {
        tempProject = Path.Combine(Path.GetTempPath(), $"AssetResolverFingerprintTests_{Guid.NewGuid():N}");
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

    private static FakeTheme Theme(params (string Path, string Content)[] files) => new(files);

    private string LocalAssetsPath => Path.Combine(tempProject, ProjectPaths.Themes, "Lumina", "Assets");

    [TestMethod]
    public void GetFingerprint_ThemeAsset_ReturnsEightLowercaseHexCharacters()
    {
        var resolver = CreateResolver();
        resolver.Initialize(Theme(("Assets/main.css", "body {}")), [], tempProject);

        var fingerprint = resolver.GetFingerprint("main.css");

        Assert.IsNotNull(fingerprint);
        Assert.MatchesRegex("^[0-9a-f]{8}$", fingerprint);
    }

    [TestMethod]
    public void GetFingerprint_IdenticalContent_ReturnsSameFingerprint()
    {
        var resolver = CreateResolver();
        resolver.Initialize(
            Theme(("Assets/main.css", "body {}"), ("Assets/copy.css", "body {}"), ("Assets/other.css", "p {}")),
            [],
            tempProject);

        Assert.AreEqual(resolver.GetFingerprint("main.css"), resolver.GetFingerprint("copy.css"));
        Assert.AreNotEqual(resolver.GetFingerprint("main.css"), resolver.GetFingerprint("other.css"));
    }

    [TestMethod]
    public void GetFingerprint_UnknownAsset_ReturnsNull()
    {
        var resolver = CreateResolver();
        resolver.Initialize(Theme(("Assets/main.css", "body {}")), [], tempProject);

        Assert.IsNull(resolver.GetFingerprint("missing.css"));
    }

    [TestMethod]
    [DataRow("/Main.css", "main.css")]
    [DataRow("FONTS\\Inter.woff2", "fonts/inter.woff2")]
    public void GetFingerprint_PathSpelling_ResolvesLikeTheAssetKey(string path, string canonical)
    {
        var resolver = CreateResolver();
        resolver.Initialize(
            Theme(("Assets/main.css", "body {}"), ("Assets/fonts/inter.woff2", "font")),
            [],
            tempProject);

        Assert.IsNotNull(resolver.GetFingerprint(path));
        Assert.AreEqual(resolver.GetFingerprint(canonical), resolver.GetFingerprint(path));
    }

    [TestMethod]
    public void GetFingerprint_LocalOverride_HashesOverrideBytes()
    {
        var theme = Theme(("Assets/main.css", "body {}"), ("Assets/changed.css", "body { color: red; }"));
        var resolver = CreateResolver();
        resolver.Initialize(theme, [], tempProject);
        var themeFingerprint = resolver.GetFingerprint("main.css");
        var changedFingerprint = resolver.GetFingerprint("changed.css");

        Directory.CreateDirectory(LocalAssetsPath);
        File.WriteAllText(Path.Combine(LocalAssetsPath, "main.css"), "body { color: red; }");
        resolver.Initialize(theme, [], tempProject);

        Assert.AreNotEqual(themeFingerprint, resolver.GetFingerprint("main.css"));
        Assert.AreEqual(changedFingerprint, resolver.GetFingerprint("main.css"));
    }

    [TestMethod]
    public void GetFingerprint_LocalOverrideWithIdenticalBytes_KeepsFingerprint()
    {
        var theme = Theme(("Assets/main.css", "body {}"));
        var resolver = CreateResolver();
        resolver.Initialize(theme, [], tempProject);
        var themeFingerprint = resolver.GetFingerprint("main.css");

        Directory.CreateDirectory(LocalAssetsPath);
        File.WriteAllText(Path.Combine(LocalAssetsPath, "main.css"), "body {}");
        resolver.Initialize(theme, [], tempProject);

        Assert.AreEqual(themeFingerprint, resolver.GetFingerprint("main.css"));
    }

    [TestMethod]
    public void GetFingerprint_FileChangedBetweenRuns_InitializeRecomputes()
    {
        var theme = Theme(("Assets/main.css", "body {}"));
        Directory.CreateDirectory(LocalAssetsPath);
        var localFile = Path.Combine(LocalAssetsPath, "site.css");
        File.WriteAllText(localFile, "v1");
        var resolver = CreateResolver();
        resolver.Initialize(theme, [], tempProject);
        var first = resolver.GetFingerprint("site.css");

        File.WriteAllText(localFile, "v2");
        var sameRun = resolver.GetFingerprint("site.css");
        resolver.Initialize(theme, [], tempProject);
        var nextRun = resolver.GetFingerprint("site.css");

        Assert.AreEqual(first, sameRun, "A render run hashes each asset once.");
        Assert.AreNotEqual(first, nextRun);
    }

    [TestMethod]
    public void GetFingerprint_ExtensionAsset_UsesPrefixedKey()
    {
        var extension = new FakeTheme([("Assets/main.css", "stats {}")], prefix: "statistics");
        var resolver = CreateResolver();
        resolver.Initialize(Theme(("Assets/main.css", "body {}")), [extension], tempProject);

        Assert.IsNotNull(resolver.GetFingerprint("statistics/main.css"));
        Assert.AreNotEqual(resolver.GetFingerprint("main.css"), resolver.GetFingerprint("statistics/main.css"));
    }

    [TestMethod]
    public async Task GetFingerprint_MatchesBytesCopiedToOutput()
    {
        var theme = Theme(("Assets/main.css", "body {}"), ("Assets/zoom.js", "zoom();"));
        Directory.CreateDirectory(LocalAssetsPath);
        await File.WriteAllTextAsync(Path.Combine(LocalAssetsPath, "main.css"), "body { margin: 0; }");
        var resolver = CreateResolver();
        resolver.Initialize(theme, [], tempProject);
        var output = Path.Combine(tempProject, "output");

        await resolver.CopyToOutputAsync(output);

        foreach (var key in new[] { "main.css", "zoom.js" })
        {
            var written = await File.ReadAllBytesAsync(Path.Combine(output, "_assets", key));
            var expected = Convert.ToHexStringLower(SHA256.HashData(written))[..8];
            Assert.AreEqual(expected, resolver.GetFingerprint(key), key);
        }
    }

    [TestMethod]
    public void GetFingerprint_ConcurrentCalls_ReturnSameValue()
    {
        var resolver = CreateResolver();
        resolver.Initialize(Theme(("Assets/main.css", "body {}")), [], tempProject);
        var results = new string?[64];

        Parallel.For(0, results.Length, i => results[i] = resolver.GetFingerprint("main.css"));

        Assert.IsNotNull(results[0]);
        Assert.IsTrue(results.All(result => result == results[0]));
    }

    private sealed class FakeTheme((string Path, string Content)[] files, string? prefix = null) : ITheme
    {
        private readonly Dictionary<string, byte[]> contents = files.ToDictionary(
            file => file.Path,
            file => Encoding.UTF8.GetBytes(file.Content),
            StringComparer.Ordinal);

        public PackageMetadata Metadata { get; } = new()
        {
            Id = prefix is null ? "Spectara.Revela.Themes.Lumina" : $"Spectara.Revela.Themes.Lumina.{prefix}",
            Name = prefix is null ? "Lumina" : $"Lumina {prefix}",
            Version = "1.0.0",
            Description = string.Empty,
            Author = "Test"
        };

        public string? Prefix { get; } = prefix;
        public string? TargetTheme { get; } = prefix is null ? null : "Lumina";
        public ThemeManifest Manifest { get; } = new() { LayoutTemplate = "Layout.revela" };

        public Stream? GetFile(string relativePath) =>
            contents.TryGetValue(relativePath, out var bytes) ? new MemoryStream(bytes, writable: false) : null;

        public IEnumerable<string> GetAllFiles() => contents.Keys;

        public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
