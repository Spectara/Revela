using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Persistence of <see cref="ImageStateStore"/> (<c>.cache/images.json</c>) on a real filesystem.
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
        await WriteCacheFileAsync(project, "manifest.json", LegacyManifest);
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
        await WriteCacheFileAsync(project, "manifest.json", LegacyManifest);
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
        await WriteCacheFileAsync(project, "manifest.json", "{ \"_meta\": ");
        var store = CreateStore(project);

        await store.LoadAsync();

        Assert.IsNull(store.Get("photos/a.jpg"));
        Assert.IsFalse(File.Exists(Path.Combine(project.RootPath, ".cache", "images.json")));
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
        Assert.IsEmpty(Directory.GetFiles(Path.Combine(project.RootPath, ".cache"), "*.tmp"));
    }

    private static ImageStateStore CreateStore(TestProject project) =>
        new(Options.Create(new ProjectEnvironment { Path = project.RootPath }), NullLogger<ImageStateStore>.Instance);

    private static async Task WriteCacheFileAsync(TestProject project, string fileName, string content)
    {
        var cacheDirectory = Path.Combine(project.RootPath, ".cache");
        Directory.CreateDirectory(cacheDirectory);
        await File.WriteAllTextAsync(Path.Combine(cacheDirectory, fileName), content);
    }
}
