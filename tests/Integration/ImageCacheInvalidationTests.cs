using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;
using Image = NetVips.Image;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Regression tests for image cache invalidation across <c>generate</c> runs.
/// </summary>
/// <remarks>
/// The scan used to write the new source size/timestamp into the manifest before image
/// processing compared against those same values, so edited sources were reported as cached
/// and kept their stale variants. Changing the theme's resize mode was not detected either.
/// </remarks>
[TestClass]
[TestCategory("E2E")]
public sealed class ImageCacheInvalidationTests
{
    private const string GalleryName = "Photos";
    private const string FileName = "photo.jpg";
    private const int VariantSize = 640;

    [TestMethod]
    public async Task ProcessAsync_SourceUnchanged_SkipsImage()
    {
        using var project = CreateProject(1600, 1000);
        var sizes = new MutableSizesProvider([VariantSize]);
        using var host = BuildHost(project, sizes);

        var first = await ScanAndProcessAsync(host);
        var second = await ScanAndProcessAsync(host);

        Assert.AreEqual(1, first.ProcessedCount);
        Assert.AreEqual(0, second.ProcessedCount);
        Assert.AreEqual(1, second.SkippedCount);
    }

    [TestMethod]
    public async Task ProcessAsync_SourceReplacedBetweenRuns_RegeneratesVariants()
    {
        using var project = CreateProject(1600, 1000);
        var sizes = new MutableSizesProvider([VariantSize]);
        using var host = BuildHost(project, sizes);
        await ScanAndProcessAsync(host);
        var sourceFile = Path.Combine(project.SourcePath, GalleryName, FileName);
        Assert.IsGreaterThan(100d, AverageRed(FindVariant(project)), "Fixture gradient must contain red so the replacement is detectable.");

        WriteSolidBlueJpeg(sourceFile, 1600, 1000);
        File.SetLastWriteTimeUtc(sourceFile, File.GetLastWriteTimeUtc(sourceFile).AddHours(1));
        var second = await ScanAndProcessAsync(host);

        Assert.AreEqual(1, second.ProcessedCount);
        Assert.IsLessThan(60d, AverageRed(FindVariant(project)), "Variant must be regenerated from the replaced source.");
    }

    [TestMethod]
    public async Task ProcessAsync_ResizeModeChangedBetweenRuns_RegeneratesVariants()
    {
        using var project = CreateProject(1000, 1600);
        var sizes = new MutableSizesProvider([VariantSize]);
        using var host = BuildHost(project, sizes);
        await ScanAndProcessAsync(host);
        Assert.AreEqual(VariantSize, VariantHeight(project), "Portrait variant is constrained by its longest side first.");

        sizes.ResizeMode = "width";
        var second = await ScanAndProcessAsync(host);

        Assert.AreEqual(1, second.ProcessedCount);
        Assert.AreEqual(VariantSize, VariantWidth(project), "Variant must be regenerated for the new resize mode.");
    }

    [TestMethod]
    [DataRow("width", 1000, 1600, true)]
    [DataRow("height", 1600, 1000, false)]
    public async Task ProcessAsync_ResizeMode_ConstrainsConfiguredSide(string resizeMode, int width, int height, bool constrainsWidth)
    {
        using var project = CreateProject(width, height);
        var sizes = new MutableSizesProvider([VariantSize]) { ResizeMode = resizeMode };
        using var host = BuildHost(project, sizes);

        await ScanAndProcessAsync(host);

        var constrainedSide = constrainsWidth ? VariantWidth(project) : VariantHeight(project);
        Assert.AreEqual(VariantSize, constrainedSide);
    }

    private static TestProject CreateProject(int width, int height) => TestProject.Create(p => p
        .WithProjectJson(new
        {
            project = new { name = "Cache" },
            theme = new { name = "Lumina" },
            generate = new { images = new { jpg = 90 } }
        })
        .WithSiteJson(new { title = "Cache", author = "Test" })
        .AddGallery(GalleryName, g => g.AddRealImage(FileName, width, height)));

    private static Microsoft.Extensions.Hosting.IHost BuildHost(TestProject project, MutableSizesProvider sizes) =>
        RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<IImageSizesProvider>(sizes);
        });

    private static async Task<ImageResult> ScanAndProcessAsync(Microsoft.Extensions.Hosting.IHost host)
    {
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scan.Success, scan.ErrorMessage);

        var images = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(images.Success, images.ErrorMessage);
        return images;
    }

    private static string FindVariant(TestProject project) => Directory
        .EnumerateFiles(Path.Combine(project.OutputPath, "images"), $"{VariantSize}.jpg", SearchOption.AllDirectories)
        .Single();

    private static int VariantWidth(TestProject project)
    {
        using var image = Image.NewFromFile(FindVariant(project), access: Enums.Access.Sequential);
        return image.Width;
    }

    private static int VariantHeight(TestProject project)
    {
        using var image = Image.NewFromFile(FindVariant(project), access: Enums.Access.Sequential);
        return image.Height;
    }

    private static double AverageRed(string path)
    {
        using var image = Image.NewFromFile(path, access: Enums.Access.Sequential);
        using var red = image[0];
        return red.Avg();
    }

    private static void WriteSolidBlueJpeg(string path, int width, int height)
    {
        using var black = Image.Black(width, height, bands: 3);
        using var blue = black + new double[] { 20, 20, 220 };
        using var pixels = blue.Cast(Enums.BandFormat.Uchar);
        pixels.Jpegsave(path, q: 90);
    }

    private sealed class MutableSizesProvider(IReadOnlyList<int> sizes) : IImageSizesProvider
    {
        public string ResizeMode { get; set; } = "longest";

        public IReadOnlyList<int> GetSizes() => sizes;

        public string GetResizeMode() => ResizeMode;
    }
}
