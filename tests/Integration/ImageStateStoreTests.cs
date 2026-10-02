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
        Assert.IsTrue(File.Exists(StatePath(project)));
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(StatePath(project))!, "*.tmp"));
    }

    private static ImageStateStore CreateStore(TestProject project) =>
        new(Options.Create(new ProjectEnvironment { Path = project.RootPath }), NullLogger<ImageStateStore>.Instance);

    private static string StatePath(TestProject project) => Path.Combine(project.RootPath, ".revela", "state", "images.json");
}
