using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Image processing state (<c>.cache/images.json</c>) decides which images are re-encoded.
/// </summary>
/// <remarks>
/// The state used to live in the scan manifest, so a manifest format change discarded it and
/// re-encoded every image (about two hours for an AVIF library on a small server) although
/// nothing about image processing had changed.
/// </remarks>
[TestClass]
[TestCategory("E2E")]
public sealed class ImageProcessingStateTests
{
    private const string GalleryName = "Photos";
    private const string GallerySlug = "photos";
    private const int VariantSize = 320;

    [TestMethod]
    [DataRow("deleted", DisplayName = "manifest deleted")]
    [DataRow("outdated", DisplayName = "manifest from another format version")]
    [DataRow("corrupt", DisplayName = "manifest unreadable")]
    public async Task ProcessAsync_ManifestDiscardedBetweenRuns_ReusesVariants(string discard)
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        var manifestPath = ManifestPath(project);

        switch (discard)
        {
            case "deleted":
                File.Delete(manifestPath);
                break;
            case "outdated":
                var manifest = ReadJson(manifestPath);
                manifest["_meta"]!["version"] = 4;
                WriteJson(manifestPath, manifest);
                break;
            default:
                await File.WriteAllTextAsync(manifestPath, "{ not json");
                break;
        }

        var rerun = await RunAsync(project);

        Assert.AreEqual(0, rerun.ProcessedCount, "A rebuilt manifest must not re-encode any image.");
        Assert.AreEqual(2, rerun.SkippedCount);
    }

    [TestMethod]
    [DataRow(90, 0, DisplayName = "same quality")]
    [DataRow(70, 2, DisplayName = "quality changed since")]
    public async Task ProcessAsync_LegacyManifestWithProcessedImages_SeedsState(int legacyQuality, int expectedProcessed)
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        ConvertToLegacyLayout(project, legacyQuality);

        var rerun = await RunAsync(project);

        Assert.AreEqual(expectedProcessed, rerun.ProcessedCount);
        Assert.IsTrue(File.Exists(StatePath(project)), "Seeding must create the image state file.");
        var meta = ReadJson(ManifestPath(project))["_meta"]!.AsObject();
        Assert.IsFalse(meta.ContainsKey("processedImages"), "The manifest no longer carries processing state.");
        Assert.IsFalse(meta.ContainsKey("formatQualities"), "The manifest no longer carries processing state.");
    }

    [TestMethod]
    public async Task ProcessAsync_LegacyManifestOfOutdatedVersion_StillSeedsState()
    {
        // The scan discards and rewrites an outdated manifest before images run;
        // the carried-over processing state must be taken before that happens.
        using var project = CreateProject("a.jpg");
        await RunAsync(project);
        ConvertToLegacyLayout(project, quality: 90);
        var manifest = ReadJson(ManifestPath(project));
        manifest["_meta"]!["version"] = 4;
        WriteJson(ManifestPath(project), manifest);

        var rerun = await RunAsync(project);

        Assert.AreEqual(0, rerun.ProcessedCount);
    }

    [TestMethod]
    public async Task ProcessAsync_StateWrittenByBeta21_ReencodesNothing()
    {
        // Libraries already processed by beta.21 must not be re-encoded by a later build with the
        // default configuration: re-encoding an AVIF library takes hours on a small server.
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        var images = new JsonObject();
        foreach (var name in new[] { "a.jpg", "b.jpg" })
        {
            var source = new FileInfo(Path.Combine(project.SourcePath, GalleryName, name));
            images[$"{GalleryName}/{name}"] = new JsonObject
            {
                ["fingerprint"] = $"v2|size:{source.Length}|mtime:{source.LastWriteTimeUtc.Ticks}|resize:longest",
                ["qualities"] = new JsonObject { ["jpg"] = 90 },
            };
        }

        var beta21State = new JsonObject { ["version"] = 1, ["images"] = images };
        Assert.AreEqual(beta21State.ToJsonString(), ReadJson(StatePath(project)).ToJsonString(), "Default settings must record exactly what beta.21 recorded.");
        WriteJson(StatePath(project), beta21State);

        var rerun = await RunAsync(project);

        Assert.AreEqual(0, rerun.ProcessedCount);
        Assert.AreEqual(2, rerun.SkippedCount);
    }

    [TestMethod]
    public async Task ProcessAsync_StateFromOlderPipelineVersion_ReencodesImage()
    {
        using var project = CreateProject("a.jpg");
        await RunAsync(project);
        var state = ReadJson(StatePath(project));
        var entry = state["images"]![$"{GalleryName}/a.jpg"]!;
        var current = $"v{NetVipsImageProcessor.OutputVersion}|";
        var fingerprint = entry["fingerprint"]!.GetValue<string>();
        Assert.StartsWith(current, fingerprint);
        entry["fingerprint"] = $"v{NetVipsImageProcessor.OutputVersion - 1}|" + fingerprint[current.Length..];
        WriteJson(StatePath(project), state);

        var rerun = await RunAsync(project);

        Assert.AreEqual(1, rerun.ProcessedCount);
    }

    [TestMethod]
    public async Task ProcessAsync_QualityChanged_ReencodesImages()
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);

        WriteProjectJson(project, jpgQuality: 70);
        var rerun = await RunAsync(project);
        var third = await RunAsync(project);

        Assert.AreEqual(2, rerun.ProcessedCount);
        Assert.AreEqual(0, third.ProcessedCount, "The new quality must be recorded.");
    }

    [TestMethod]
    public async Task ProcessAsync_SizeAdded_GeneratesNewVariants()
    {
        using var project = CreateProject("a.jpg");
        await RunAsync(project);

        var rerun = await RunAsync(project, [VariantSize, VariantSize / 2]);

        Assert.AreEqual(1, rerun.ProcessedCount);
        Assert.IsTrue(File.Exists(VariantPath(project, "a", VariantSize / 2)));
    }

    [TestMethod]
    public async Task ProcessAsync_SourceContentChanged_ReencodesOnlyThatImage()
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        var source = Path.Combine(project.SourcePath, GalleryName, "a.jpg");
        TestImageGenerator.CreateJpeg(source, 1200, 900);
        File.SetLastWriteTimeUtc(source, File.GetLastWriteTimeUtc(source).AddHours(1));

        var rerun = await RunAsync(project);

        Assert.AreEqual(1, rerun.ProcessedCount);
        Assert.AreEqual(1, rerun.SkippedCount);
    }

    [TestMethod]
    public async Task ProcessAsync_VariantFileDeleted_ReencodesThatImage()
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        var variant = VariantPath(project, "a", VariantSize);
        File.Delete(variant);

        var rerun = await RunAsync(project);

        Assert.AreEqual(1, rerun.ProcessedCount);
        Assert.AreEqual(1, rerun.SkippedCount);
        Assert.IsTrue(File.Exists(variant));
    }

    [TestMethod]
    public async Task ProcessAsync_OutputDeleted_ReencodesAllImages()
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        Directory.Delete(project.OutputPath, recursive: true);

        var rerun = await RunAsync(project);

        Assert.AreEqual(2, rerun.ProcessedCount);
        Assert.IsTrue(File.Exists(VariantPath(project, "b", VariantSize)));
    }

    [TestMethod]
    public async Task ProcessAsync_CacheDeleted_ReencodesAllImages()
    {
        using var project = CreateProject("a.jpg");
        await RunAsync(project);
        Directory.Delete(Path.Combine(project.RootPath, ".cache"), recursive: true);

        var rerun = await RunAsync(project);

        Assert.AreEqual(1, rerun.ProcessedCount);
    }

    [TestMethod]
    [DataRow("{ not json", DisplayName = "corrupt")]
    [DataRow(/*lang=json,strict*/ """{ "version": 999, "images": {} }""", DisplayName = "unknown version")]
    [DataRow(/*lang=json,strict*/ """{ "version": 1, "images": { "Photos/a.jpg": null } }""", DisplayName = "null entry")]
    [DataRow("null", DisplayName = "null document")]
    public async Task ProcessAsync_UnusableStateFile_ReprocessesAllAndRewritesState(string content)
    {
        using var project = CreateProject("a.jpg");
        await RunAsync(project);
        await File.WriteAllTextAsync(StatePath(project), content);

        var rerun = await RunAsync(project);
        var third = await RunAsync(project);

        Assert.AreEqual(1, rerun.ProcessedCount);
        Assert.AreEqual(0, third.ProcessedCount, "The rewritten state must be usable again.");
    }

    [TestMethod]
    public async Task ProcessAsync_SourceDeleted_DropsItsState()
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        File.Delete(Path.Combine(project.SourcePath, GalleryName, "b.jpg"));

        await RunAsync(project);

        var images = ReadJson(StatePath(project))["images"]!.AsObject();
        Assert.IsTrue(images.ContainsKey($"{GalleryName}/a.jpg"));
        Assert.IsFalse(images.ContainsKey($"{GalleryName}/b.jpg"));
    }

    /// <summary>
    /// Rewrites the cache as beta.21 left it: processing state inside the manifest, no state file.
    /// </summary>
    private static void ConvertToLegacyLayout(TestProject project, int quality)
    {
        var state = ReadJson(StatePath(project))["images"]!.AsObject();
        var processedImages = new JsonObject();
        foreach (var (path, entry) in state)
        {
            processedImages[path] = entry!["fingerprint"]!.GetValue<string>();
        }

        var manifest = ReadJson(ManifestPath(project));
        manifest["_meta"]!["processedImages"] = processedImages;
        manifest["_meta"]!["formatQualities"] = new JsonObject { ["jpg"] = quality };
        WriteJson(ManifestPath(project), manifest);
        File.Delete(StatePath(project));
    }

    private static TestProject CreateProject(params string[] fileNames)
    {
        var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "State", author = "Test" })
            .AddGallery(GalleryName, g =>
            {
                foreach (var fileName in fileNames)
                {
                    g.AddRealImage(fileName, 640, 480);
                }
            }));
        WriteProjectJson(project, jpgQuality: 90);
        return project;
    }

    private static void WriteProjectJson(TestProject project, int jpgQuality) =>
        WriteJson(Path.Combine(project.RootPath, "project.json"), new JsonObject
        {
            ["project"] = new JsonObject { ["name"] = "State" },
            ["theme"] = new JsonObject { ["name"] = "Lumina" },
            ["generate"] = new JsonObject { ["images"] = new JsonObject { ["jpg"] = jpgQuality } },
        });

    private static async Task<ImageResult> RunAsync(TestProject project, IReadOnlyList<int>? sizes = null)
    {
        using var host = BuildHost(project, sizes ?? [VariantSize]);
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scan.Success, scan.ErrorMessage);

        var images = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(images.Success, images.ErrorMessage);
        return images;
    }

    private static IHost BuildHost(TestProject project, IReadOnlyList<int> sizes) =>
        RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<IImageSizesProvider>(new FixedSizesProvider(sizes));
        });

    private static string ManifestPath(TestProject project) => Path.Combine(project.RootPath, ".cache", "manifest.json");

    private static string StatePath(TestProject project) => Path.Combine(project.RootPath, ".cache", "images.json");

    private static string VariantPath(TestProject project, string imageSlug, int size) =>
        Path.Combine(project.OutputPath, "images", GallerySlug, imageSlug, $"{size}.jpg");

    private static JsonObject ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static void WriteJson(string path, JsonNode node) => File.WriteAllText(path, node.ToJsonString());

    private sealed class FixedSizesProvider(IReadOnlyList<int> sizes) : IImageSizesProvider
    {
        public IReadOnlyList<int> GetSizes() => sizes;

        public string GetResizeMode() => "longest";
    }
}
