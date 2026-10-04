using System.Security.Cryptography;
using System.Text;
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
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Image processing state (<c>.revela/core/images.json</c>) decides which images are re-encoded.
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
    public async Task ProcessAsync_StateWrittenByBeta21_ReencodesNothing()
    {
        // JPEG/WebP libraries already processed by beta.21 must not be re-encoded by a later
        // build with the default configuration.
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        var recorded = ReadJson(StatePath(project)).ToJsonString();
        var beta21State = WriteBeta21State(project, new JsonObject { ["jpg"] = 90 });
        Assert.AreEqual(beta21State.ToJsonString(), recorded, "Default settings must record exactly what beta.21 recorded.");

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
    public async Task ProcessAsync_ScanMetadataFromOlderVersion_RereadsMetadataWithoutReencoding()
    {
        // A MetadataVersion bump (e.g. the average colour replacing the CSS LQIP hash) re-reads
        // the scan metadata once; the processing state is independent of it, so no image is
        // re-encoded.
        using var project = CreateProject("a.jpg");
        await RunAsync(project);
        var manifest = ReadJson(ManifestPath(project));
        var input = $"metadata:{NetVipsImageProcessor.MetadataVersion - 1}|minWidth:0|minHeight:0";
        manifest["_meta"]!["scanConfigHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..12];
        var image = manifest["root"]!["children"]![0]!["content"]!.AsArray().Single(c => (string?)c!["filename"] == "a.jpg")!;
        var color = image["color"]!.GetValue<string>();
        image.AsObject().Remove("color");
        image["placeholder"] = "-721311";
        WriteJson(ManifestPath(project), manifest);

        var rerun = await RunAsync(project);

        Assert.AreEqual(0, rerun.ProcessedCount, "A metadata rescan must not re-encode any image.");
        var rescanned = ReadJson(ManifestPath(project))["root"]!["children"]![0]!["content"]!.AsArray()
            .Single(c => (string?)c!["filename"] == "a.jpg")!;
        Assert.AreEqual(color, rescanned["color"]!.GetValue<string>(), "The scan must re-read the colour.");
        Assert.IsNull(rescanned["placeholder"], "The manifest must drop the old LQIP hash.");
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
    public async Task ProcessAsync_AvifEffortChanged_ReencodesOnlyAvif()
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["avif"] = 60 });
        await RunAsync(project);
        var jpg = VariantPath(project, "a", VariantSize);
        var avif = Path.ChangeExtension(jpg, ".avif");
        File.SetLastWriteTimeUtc(jpg, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(avif, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var defaultEffortAvif = await File.ReadAllBytesAsync(avif);
        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["avif"] = 60, ["avifEffort"] = 0 });
        var rerun = await RunAsync(project);
        var third = await RunAsync(project);

        Assert.AreEqual(2, rerun.ProcessedCount);
        Assert.AreEqual(4, rerun.FilesCreated, "Only the AVIF variants (320 and the original width) are re-encoded.");
        Assert.AreEqual(2020, File.GetLastWriteTimeUtc(jpg).Year, "JPG variants must be kept.");
        Assert.AreNotEqual(2020, File.GetLastWriteTimeUtc(avif).Year, "AVIF variants must be re-encoded.");
        CollectionAssert.AreNotEqual(defaultEffortAvif, await File.ReadAllBytesAsync(avif), "The encoder must use the configured effort.");
        Assert.AreEqual(0, third.ProcessedCount, "The new effort must be recorded.");
    }

    [TestMethod]
    public async Task ProcessAsync_EffortSetBackToDefault_ReencodesAgain()
    {
        using var project = CreateProject("a.jpg");
        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["webp"] = 80, ["webpEffort"] = 1 });
        await RunAsync(project);

        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["webp"] = 80 });
        var rerun = await RunAsync(project);

        Assert.AreEqual(1, rerun.ProcessedCount);
        Assert.AreEqual(2, rerun.FilesCreated, "Only the WebP variants are re-encoded.");
    }

    [TestMethod]
    public async Task ProcessAsync_DefaultEffortsWrittenExplicitly_ReencodesNothing()
    {
        using var project = CreateProject("a.jpg");
        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["avif"] = 60 });
        await RunAsync(project);
        var state = await File.ReadAllTextAsync(StatePath(project));

        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["avif"] = 60, ["avifEffort"] = 2, ["webpEffort"] = 4 });
        var rerun = await RunAsync(project);

        Assert.AreEqual(0, rerun.ProcessedCount);
        Assert.AreEqual(state, await File.ReadAllTextAsync(StatePath(project)));
    }

    [TestMethod]
    public async Task ProcessAsync_AvifStateWrittenByBeta21_ReencodesOnlyAvifOnce()
    {
        // beta.21 encoded AVIF with libvips' effort 4 and recorded no effort. The default is now
        // effort 2, so AVIF variants are re-encoded once; JPG variants are kept.
        using var project = CreateProject("a.jpg", "b.jpg");
        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["avif"] = 60 });
        await RunAsync(project);
        WriteBeta21State(project, new JsonObject { ["jpg"] = 90, ["avif"] = 60 });
        var jpg = VariantPath(project, "a", VariantSize);
        File.SetLastWriteTimeUtc(jpg, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var rerun = await RunAsync(project);
        var third = await RunAsync(project);

        Assert.AreEqual(2, rerun.ProcessedCount);
        Assert.AreEqual(4, rerun.FilesCreated, "Only the AVIF variants (320 and the original width) are re-encoded.");
        Assert.AreEqual(2020, File.GetLastWriteTimeUtc(jpg).Year, "JPG variants must be kept.");
        Assert.AreEqual(0, third.ProcessedCount, "The new effort must be recorded.");
    }

    [TestMethod]
    public async Task ProcessAsync_AvifStateWrittenByBeta21WithExplicitEffort4_ReencodesNothing()
    {
        // Setting the previous effort explicitly keeps an AVIF library as it is.
        using var project = CreateProject("a.jpg");
        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["avif"] = 60, ["avifEffort"] = 4 });
        await RunAsync(project);
        WriteBeta21State(project, new JsonObject { ["jpg"] = 90, ["avif"] = 60 });

        var rerun = await RunAsync(project);

        Assert.AreEqual(0, rerun.ProcessedCount);
    }

    [TestMethod]
    public async Task ProcessAsync_MaxSizeSet_CapsTheLargestVariantOfLargerImages()
    {
        using var project = CreateProject(("wide.jpg", 640, 480), ("tall.jpg", 480, 640), ("small.jpg", 300, 200));
        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["maxSize"] = 400 });

        await RunAsync(project);

        var sizes = ReadManifestSizes(project);
        Assert.AreEqual("320,400", sizes["wide.jpg"]);
        Assert.AreEqual("320,400", sizes["tall.jpg"], "A size is the longest edge, also for portraits.");
        Assert.AreEqual("300", sizes["small.jpg"], "Images within the cap keep their full resolution.");
        AssertDimensions(VariantPath(project, "wide", 400), 400, 300);
        AssertDimensions(VariantPath(project, "tall", 400), 300, 400);
        Assert.IsFalse(File.Exists(VariantPath(project, "wide", 640)));
    }

    [TestMethod]
    public async Task ProcessAsync_MaxSizeChanged_ReencodesOnlyImagesAboveTheCap()
    {
        using var project = CreateProject(("wide.jpg", 640, 480), ("small.jpg", 300, 200));
        await RunAsync(project);

        WriteImageSettings(project, new JsonObject { ["jpg"] = 90, ["maxSize"] = 400 });
        var capped = await RunAsync(project);
        WriteImageSettings(project, new JsonObject { ["jpg"] = 90 });
        var uncapped = await RunAsync(project);

        Assert.AreEqual(1, capped.ProcessedCount);
        Assert.AreEqual(1, capped.SkippedCount);
        Assert.AreEqual(1, uncapped.ProcessedCount);
        AssertDimensions(VariantPath(project, "wide", 640), 640, 480);
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
    public async Task ProcessAsync_AfterCleanCache_ReusesVariants()
    {
        // The manifest is a cache artifact; the image state belongs to the processed images (output).
        using var project = CreateProject("a.jpg");
        await RunAsync(project);
        await CleanAsync(project, ArtifactKind.Cache);

        Assert.IsFalse(File.Exists(ManifestPath(project)));
        var rerun = await RunAsync(project);

        Assert.AreEqual(0, rerun.ProcessedCount);
        Assert.AreEqual(1, rerun.SkippedCount);
    }

    [TestMethod]
    public async Task ProcessAsync_AfterCleanOutput_ReencodesAllImages()
    {
        using var project = CreateProject("a.jpg", "b.jpg");
        await RunAsync(project);
        await CleanAsync(project, ArtifactKind.Output);

        Assert.IsFalse(File.Exists(StatePath(project)), "The image state goes with the processed images.");
        Assert.IsTrue(File.Exists(ManifestPath(project)), "The manifest stays.");
        var rerun = await RunAsync(project);

        Assert.AreEqual(2, rerun.ProcessedCount);
    }

    [TestMethod]
    public async Task ProcessAsync_StateDeleted_ReencodesAllImages()
    {
        using var project = CreateProject("a.jpg");
        await RunAsync(project);
        File.Delete(StatePath(project));

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
    /// Replaces the state with what beta.21 recorded for the project's images: no efforts,
    /// because beta.21 always encoded with libvips' default effort.
    /// </summary>
    private static JsonObject WriteBeta21State(TestProject project, JsonObject qualities)
    {
        var images = new JsonObject();
        foreach (var source in new DirectoryInfo(Path.Combine(project.SourcePath, GalleryName)).GetFiles("*.jpg").OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            images[$"{GalleryName}/{source.Name}"] = new JsonObject
            {
                ["fingerprint"] = $"v2|size:{source.Length}|mtime:{source.LastWriteTimeUtc.Ticks}|resize:longest",
                ["qualities"] = qualities.DeepClone(),
            };
        }

        var state = new JsonObject { ["version"] = 1, ["images"] = images };
        WriteJson(StatePath(project), state);
        return state;
    }

    private static TestProject CreateProject(params string[] fileNames) =>
        CreateProject([.. fileNames.Select(fileName => (fileName, 640, 480))]);

    private static TestProject CreateProject(params (string FileName, int Width, int Height)[] images)
    {
        var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "State", author = "Test" })
            .AddGallery(GalleryName, g =>
            {
                foreach (var (fileName, width, height) in images)
                {
                    g.AddRealImage(fileName, width, height);
                }
            }));
        WriteProjectJson(project, jpgQuality: 90);
        return project;
    }

    private static Dictionary<string, string> ReadManifestSizes(TestProject project) =>
        ReadJson(ManifestPath(project))["root"]!["children"]![0]!["content"]!.AsArray()
            .ToDictionary(c => c!["filename"]!.GetValue<string>(), c => string.Join(",", c!["sizes"]!.AsArray().Select(s => s!.GetValue<int>())));

    private static void AssertDimensions(string path, int width, int height)
    {
        using var image = NetVips.Image.NewFromFile(path);
        Assert.AreEqual((width, height), (image.Width, image.Height), Path.GetFileName(path));
    }

    private static void WriteProjectJson(TestProject project, int jpgQuality) =>
        WriteImageSettings(project, new JsonObject { ["jpg"] = jpgQuality });

    private static void WriteImageSettings(TestProject project, JsonObject images) =>
        WriteJson(Path.Combine(project.RootPath, "project.json"), new JsonObject
        {
            ["project"] = new JsonObject { ["name"] = "State" },
            ["theme"] = new JsonObject { ["name"] = "Lumina" },
            ["generate"] = new JsonObject { ["images"] = images },
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

    private static async Task CleanAsync(TestProject project, ArtifactKind kind)
    {
        using var host = BuildHost(project, [VariantSize]);
        var result = await host.Services.GetRequiredService<IArtifactLifecycle>().InvalidateAllAsync([kind]);
        Assert.IsTrue(result.Success, result.ErrorMessage);
    }

    private static IHost BuildHost(TestProject project, IReadOnlyList<int> sizes) =>
        RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<IImageSizesProvider>(new FixedSizesProvider(sizes));
        });

    private static string ManifestPath(TestProject project) => Path.Combine(project.RootPath, ".revela", "core", "manifest.json");

    private static string StatePath(TestProject project) => Path.Combine(project.RootPath, ".revela", "core", "images.json");

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
