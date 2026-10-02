using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Persistence of <see cref="ImageStateStore"/> (<c>.revela/state/images.json</c>) on a real filesystem.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ImageStateStoreTests
{
    private const string LegacyManifest = /*lang=json,strict*/ """
        {
          "_meta": {
            "version": 5,
            "formatQualities": { "avif": 60, "jpg": 85 },
            "processedImages": { "photos/a.jpg": "v2|legacy", "photos/b.jpg": "" }
          },
          "root": null
        }
        """;

    [TestMethod]
    public async Task LoadAsync_OnlyLegacyManifest_SeedsFingerprintsAndQualities()
    {
        using var project = TestProject.Create();
        await WriteLegacyCacheFileAsync(project, "manifest.json", LegacyManifest);
        var store = CreateStore(project);

        await store.LoadAsync();

        var seeded = store.Get("photos/a.jpg");
        Assert.IsNotNull(seeded);
        Assert.AreEqual("v2|legacy", seeded.Fingerprint);
        Assert.AreEqual(60, seeded.Qualities["avif"]);
        Assert.AreEqual(85, seeded.Qualities["jpg"]);
        Assert.IsNull(store.Get("photos/b.jpg"), "Entries without a fingerprint are not carried over.");
    }

    [TestMethod]
    public async Task LoadAsync_StateFileExists_IgnoresLegacyManifest()
    {
        using var project = TestProject.Create();
        await WriteLegacyCacheFileAsync(project, "manifest.json", LegacyManifest);
        var writer = CreateStore(project);
        writer.Set("photos/a.jpg", new ProcessedImage { Fingerprint = "v2|current" });
        await writer.SaveAsync();
        var store = CreateStore(project);

        await store.LoadAsync();

        Assert.AreEqual("v2|current", store.Get("photos/a.jpg")?.Fingerprint);
    }

    [TestMethod]
    public async Task LoadAsync_CorruptManifestAndNoState_StartsEmptyWithoutStateFile()
    {
        using var project = TestProject.Create();
        await WriteLegacyCacheFileAsync(project, "manifest.json", "{ \"_meta\": ");
        var store = CreateStore(project);

        await store.LoadAsync();

        Assert.IsNull(store.Get("photos/a.jpg"));
        Assert.IsFalse(File.Exists(StatePath(project)));
    }

    [TestMethod]
    public async Task SaveAsync_ConcurrentSets_PersistsEveryImage()
    {
        using var project = TestProject.Create();
        var writer = CreateStore(project);
        Parallel.For(0, 500, i => writer.Set(
            $"photos/{i:D3}.jpg",
            new ProcessedImage { Fingerprint = $"v2|{i}", Qualities = new Dictionary<string, int> { ["jpg"] = 85 } }));

        await writer.SaveAsync();
        var store = CreateStore(project);
        await store.LoadAsync();

        Assert.AreEqual("v2|0", store.Get("photos/000.jpg")?.Fingerprint);
        Assert.AreEqual("v2|499", store.Get("photos/499.jpg")?.Fingerprint);
        Assert.AreEqual(85, store.Get("photos/250.jpg")?.Qualities["jpg"]);
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(StatePath(project))!, "*.tmp"));
    }

    [TestMethod]
    public async Task LoadAsync_LegacyStateFile_MovesItToStateFolder()
    {
        using var project = TestProject.Create();
        await WriteLegacyCacheFileAsync(project, "images.json", /*lang=json,strict*/ """
            { "version": 1, "images": { "photos/a.jpg": { "fingerprint": "v2|moved", "qualities": { "jpg": 85 } } } }
            """);
        var store = CreateStore(project);

        await store.LoadAsync();

        Assert.AreEqual("v2|moved", store.Get("photos/a.jpg")?.Fingerprint);
        Assert.IsTrue(File.Exists(StatePath(project)));
        Assert.IsFalse(Directory.Exists(LegacyCachePath(project)), "The rest of the legacy cache moves to .revela/cache.");
    }

    [TestMethod]
    public async Task LoadAsync_LegacyAndCurrentCacheBothExist_CarriesStateAndLeavesLegacyFolder()
    {
        using var project = TestProject.Create();
        await WriteLegacyCacheFileAsync(project, "manifest.json", LegacyManifest);
        var currentManifest = Path.Combine(project.RootPath, ".revela", "cache", "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(currentManifest)!);
        await File.WriteAllTextAsync(currentManifest, "{}");
        var store = CreateStore(project);

        await store.LoadAsync();

        Assert.AreEqual("v2|legacy", store.Get("photos/a.jpg")?.Fingerprint);
        Assert.AreEqual("{}", await File.ReadAllTextAsync(currentManifest), "The current cache must not be replaced.");
        Assert.IsTrue(File.Exists(Path.Combine(LegacyCachePath(project), "manifest.json")), "Nothing is merged or deleted.");
    }

    [TestMethod]
    public async Task LoadAsync_LegacyStateFileButStateExists_KeepsCurrentState()
    {
        using var project = TestProject.Create();
        var writer = CreateStore(project);
        writer.Set("photos/a.jpg", new ProcessedImage { Fingerprint = "v2|current" });
        await writer.SaveAsync();
        await WriteLegacyCacheFileAsync(project, "images.json", /*lang=json,strict*/ """
            { "version": 1, "images": { "photos/a.jpg": { "fingerprint": "v2|stale" } } }
            """);
        var store = CreateStore(project);

        await store.LoadAsync();

        Assert.AreEqual("v2|current", store.Get("photos/a.jpg")?.Fingerprint);
        Assert.IsFalse(File.Exists(Path.Combine(project.RootPath, ".revela", "cache", "images.json")), "The superseded legacy state is dropped.");
    }

    private static ImageStateStore CreateStore(TestProject project) =>
        new(Options.Create(new ProjectEnvironment { Path = project.RootPath }), NullLogger<ImageStateStore>.Instance);

    private static string StatePath(TestProject project) => Path.Combine(project.RootPath, ".revela", "state", "images.json");

    private static string LegacyCachePath(TestProject project) => Path.Combine(project.RootPath, ".cache");

    private static async Task WriteLegacyCacheFileAsync(TestProject project, string fileName, string content)
    {
        var cacheDirectory = LegacyCachePath(project);
        Directory.CreateDirectory(cacheDirectory);
        await File.WriteAllTextAsync(Path.Combine(cacheDirectory, fileName), content);
    }
}
