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
/// End-to-end tests for color management of published image variants.
/// </summary>
/// <remarks>
/// Variants are saved without metadata, so browsers interpret them as sRGB. Sources in other
/// color spaces (Display P3, CMYK) must therefore be converted, not merely stripped of their
/// profile — otherwise every wide-gamut photo is shown desaturated and CMYK photos break.
/// </remarks>
[TestClass]
[TestCategory("E2E")]
public sealed class ColorManagementTests
{
    private const string GalleryName = "Photos";
    private static readonly double[] SrgbColor = [200, 40, 40];

    [TestMethod]
    public async Task ProcessAsync_DisplayP3Source_VariantsMatchSrgbColor()
    {
        using var project = CreateProject(g => g.AddRealImage("placeholder.jpg", 800, 600));
        var source = SourceFile(project, "placeholder.jpg");
        WriteSolidJpeg(source, SrgbColor, "p3");
        using var host = BuildHost(project);

        await ScanAndProcessAsync(host);

        foreach (var variant in Variants(project))
        {
            var mean = MeanColor(variant);
            for (var band = 0; band < 3; band++)
            {
                Assert.IsLessThan(3d, Math.Abs(mean[band] - SrgbColor[band]), $"{Path.GetFileName(variant)} band {band}: {mean[band]:F1}");
            }
        }
    }

    [TestMethod]
    public async Task ProcessAsync_CmykSource_VariantsAreThreeBandSrgb()
    {
        using var project = CreateProject(g => g.AddRealImage("placeholder.jpg", 800, 600));
        var source = SourceFile(project, "placeholder.jpg");
        WriteSolidJpeg(source, SrgbColor, "cmyk");
        using (var check = Image.NewFromFile(source))
        {
            Assert.AreEqual(Enums.Interpretation.Cmyk, check.Interpretation, "Fixture must be a CMYK JPEG.");
        }

        using var host = BuildHost(project);

        await ScanAndProcessAsync(host);

        var variants = Variants(project);
        Assert.IsNotEmpty(variants);
        foreach (var variant in variants)
        {
            using var image = Image.NewFromFile(variant);
            Assert.AreEqual(3, image.Bands, Path.GetFileName(variant));
            Assert.AreEqual(Enums.Interpretation.Srgb, image.Interpretation, Path.GetFileName(variant));
        }
    }

    [TestMethod]
    public async Task ScanAsync_DisplayP3Source_PlaceholderMatchesSrgbEquivalent()
    {
        double[] saturatedGreen = [20, 200, 60];
        using var project = CreateProject(g => g
            .AddRealImage("srgb.jpg", 800, 600)
            .AddRealImage("p3.jpg", 800, 600)
            .AddRealImage("p3-untagged.jpg", 800, 600));
        WriteSolidJpeg(SourceFile(project, "srgb.jpg"), saturatedGreen, profile: null);
        WriteSolidJpeg(SourceFile(project, "p3.jpg"), saturatedGreen, "p3");
        WriteSolidJpeg(SourceFile(project, "p3-untagged.jpg"), saturatedGreen, "p3", keepProfile: false);
        using var host = BuildHost(project);

        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();

        Assert.IsTrue(scan.Success, scan.ErrorMessage);
        var images = host.Services.GetRequiredService<IManifestRepository>().Images.Values.ToList();
        string? Placeholder(string fileName) => images.Single(i => i.Filename == fileName).Placeholder;
        Assert.IsNotNull(Placeholder("srgb.jpg"));
        Assert.AreNotEqual(Placeholder("srgb.jpg"), Placeholder("p3-untagged.jpg"), "Control: raw P3 values must yield a different placeholder.");
        Assert.AreEqual(Placeholder("srgb.jpg"), Placeholder("p3.jpg"));
    }

    [TestMethod]
    public async Task ProcessAsync_UltraHdrSource_ProducesSdrVariants()
    {
        using var project = CreateProject(g => g.AddRealImage("placeholder.jpg", 800, 600));
        var source = SourceFile(project, "placeholder.jpg");
        WriteUltraHdrJpeg(source, 800, 600);
        using (var check = Image.NewFromFile(source))
        {
            Assert.IsTrue(check.Contains("gainmap-data"), "Fixture must be an Ultra HDR JPEG with a gain map.");
        }

        using var host = BuildHost(project);

        await ScanAndProcessAsync(host);

        var jpegs = Variants(project).Where(v => v.EndsWith(".jpg", StringComparison.Ordinal)).ToList();
        Assert.IsNotEmpty(jpegs);
        foreach (var variant in jpegs)
        {
            using var image = Image.NewFromFile(variant);
            Assert.IsFalse(image.Contains("gainmap-data"), $"{Path.GetFileName(variant)} must be a plain SDR JPEG.");
            Assert.AreEqual(3, image.Bands);
        }
    }

    private static TestProject CreateProject(Action<TestProject.GalleryBuilder> gallery) => TestProject.Create(p => p
        .WithProjectJson(new
        {
            project = new { name = "Color" },
            theme = new { name = "Lumina" },
            generate = new { images = new { jpg = 95, webp = 95 } }
        })
        .WithSiteJson(new { title = "Color", author = "Test" })
        .AddGallery(GalleryName, gallery));

    private static Microsoft.Extensions.Hosting.IHost BuildHost(TestProject project) =>
        RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<IImageSizesProvider>(new FixedSizesProvider([320]));
        });

    private static async Task ScanAndProcessAsync(Microsoft.Extensions.Hosting.IHost host)
    {
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scan.Success, scan.ErrorMessage);

        var images = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(images.Success, images.ErrorMessage);
    }

    private static string SourceFile(TestProject project, string fileName) =>
        Path.Combine(project.SourcePath, GalleryName, fileName);

    private static List<string> Variants(TestProject project) => [.. Directory
        .EnumerateFiles(Path.Combine(project.OutputPath, "images"), "*.*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)];

    private static double[] MeanColor(string path)
    {
        using var image = Image.NewFromFile(path);
        return [.. Enumerable.Range(0, 3).Select(band =>
        {
            using var channel = image[band];
            return channel.Avg();
        })];
    }

    /// <summary>
    /// Writes a solid color given in sRGB, optionally converted to (and tagged with) another profile.
    /// </summary>
    private static void WriteSolidJpeg(string path, double[] srgb, string? profile, bool keepProfile = true)
    {
        using var black = Image.Black(800, 600, bands: 3);
        using var colored = black + srgb;
        using var pixels = colored.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        if (profile is null)
        {
            pixels.Jpegsave(path, q: 95);
            return;
        }

        using var converted = pixels.IccTransform(profile, inputProfile: "srgb", intent: Enums.Intent.Relative);
        converted.Jpegsave(path, q: 95, keep: keepProfile ? Enums.ForeignKeep.All : Enums.ForeignKeep.None);
    }

    /// <summary>
    /// Writes an Ultra HDR JPEG (SDR base image + gain map) from linear scRGB with highlights above SDR white.
    /// </summary>
    private static void WriteUltraHdrJpeg(string path, int width, int height)
    {
        using var xyz = Image.Xyz(width, height);
        using var x = xyz[0];
        using var ramp = x / width * 4.0;
        using var hdr = ramp.Bandjoin(ramp * 0.8, ramp * 0.6)
            .Cast(Enums.BandFormat.Float)
            .Copy(interpretation: Enums.Interpretation.Scrgb);
        hdr.WriteToFile(path);
    }

    private sealed class FixedSizesProvider(IReadOnlyList<int> sizes) : IImageSizesProvider
    {
        public IReadOnlyList<int> GetSizes() => sizes;

        public string GetResizeMode() => "longest";
    }
}
