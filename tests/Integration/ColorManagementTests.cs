using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Features.Generate.Services;
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
    public async Task ProcessAsync_SrgbTaggedSource_VariantsMatchConvertedReference()
    {
        // Skipping a conversion that changes no pixel must not change a single byte.
        using var project = CreateProject(g => g.AddRealImage("placeholder.jpg", 800, 600));
        var source = SourceFile(project, "placeholder.jpg");
        WriteGradientJpeg(source, 800, 600, "srgb");
        using var host = BuildHost(project);

        await ScanAndProcessAsync(host);

        var variants = Variants(project);
        Assert.HasCount(4, variants, "320 and the original width, as JPG and WebP.");
        using var loaded = Image.NewFromFile(source);
        using var original = loaded.Autorot();
        foreach (var variant in variants)
        {
            var width = int.Parse(Path.GetFileNameWithoutExtension(variant), System.Globalization.CultureInfo.InvariantCulture);
            using var resized = width >= original.Width ? original.Copy() : original.ThumbnailImage(width);
            using var converted = resized.IccTransform("srgb", embedded: true, intent: Enums.Intent.Perceptual);
            var expected = variant.EndsWith(".webp", StringComparison.Ordinal)
                ? converted.WebpsaveBuffer(q: 95, keep: Enums.ForeignKeep.None)
                : converted.JpegsaveBuffer(q: 95, keep: Enums.ForeignKeep.None);

            CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(variant), Path.GetFileName(variant));
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
    public async Task ScanAsync_DisplayP3Source_ColorMatchesSrgbEquivalent()
    {
        double[] saturatedGreen = [20, 200, 60];
        using var project = CreateProject(g => g
            .AddRealImage("srgb.jpg", 800, 600)
            .AddRealImage("p3.jpg", 800, 600)
            .AddRealImage("p3-untagged.jpg", 800, 600));
        WriteSolidJpeg(SourceFile(project, "srgb.jpg"), saturatedGreen, profile: null);
        WriteSolidJpeg(SourceFile(project, "p3.jpg"), saturatedGreen, "p3");
        WriteSolidJpeg(SourceFile(project, "p3-untagged.jpg"), saturatedGreen, "p3", keepProfile: false);

        var colors = await ScanColorsAsync(project);

        AssertColorNear(saturatedGreen, colors["srgb.jpg"], tolerance: 3);
        AssertColorNear(saturatedGreen, colors["p3.jpg"], tolerance: 3);
        Assert.IsGreaterThan(10, MaxChannelDistance(saturatedGreen, colors["p3-untagged.jpg"]), "Control: raw P3 values must yield a different colour.");
    }

    [TestMethod]
    [DataRow(200d, 40d, 40d)]
    [DataRow(12d, 34d, 250d)]
    [DataRow(128d, 128d, 128d)]
    public async Task ScanAsync_SolidSource_ColorIsLowercaseHexOfThatColor(double red, double green, double blue)
    {
        double[] srgb = [red, green, blue];
        using var project = CreateProject(g => g.AddRealImage("solid.jpg", 800, 600));
        WriteSolidJpeg(SourceFile(project, "solid.jpg"), srgb, profile: null);

        var colors = await ScanColorsAsync(project);

        Assert.MatchesRegex("^#[0-9a-f]{6}$", colors["solid.jpg"]);
        AssertColorNear(srgb, colors["solid.jpg"], tolerance: 2);
    }

    [TestMethod]
    public async Task ScanAsync_BlackAndWhiteHalves_ColorIsOklabAverage()
    {
        // The Oklab mean of black and white is L 0.5: sRGB #636363, darker than the mean of the
        // gamma-encoded values (#808080) and close to how the halves look side by side.
        using var project = CreateProject(g => g.AddRealImage("halves.jpg", 800, 600));
        WriteHalvesJpeg(SourceFile(project, "halves.jpg"), [0, 0, 0], [255, 255, 255]);

        var colors = await ScanColorsAsync(project);

        AssertColorNear([99, 99, 99], colors["halves.jpg"], tolerance: 8);
    }

    [TestMethod]
    [DataRow(0d, 0d, 0d, "#000000")]
    [DataRow(1d, 0d, 0d, "#ffffff")]
    [DataRow(0.5d, 0d, 0d, "#636363")]
    [DataRow(0.627955d, 0.224863d, 0.125846d, "#ff0000")]
    [DataRow(0.7d, 0.4d, 0d, "#ff0094")]
    public void OklabToSrgbHex_KnownColors_ReturnsClampedLowercaseHex(double lightness, double a, double b, string expected) =>
        Assert.AreEqual(expected, NetVipsImageProcessor.OklabToSrgbHex(lightness, a, b));

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

    private static async Task<Dictionary<string, string>> ScanColorsAsync(TestProject project)
    {
        using var host = BuildHost(project);
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scan.Success, scan.ErrorMessage);

        return host.Services.GetRequiredService<IManifestRepository>().Images.Values
            .ToDictionary(i => i.Filename, i => i.Color ?? throw new AssertFailedException($"{i.Filename} has no colour."), StringComparer.Ordinal);
    }

    private static double MaxChannelDistance(double[] expected, string hex) =>
        Enumerable.Range(0, 3).Max(band =>
            Math.Abs(expected[band] - int.Parse(hex.AsSpan(1 + (band * 2), 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)));

    private static void AssertColorNear(double[] expected, string hex, int tolerance) =>
        Assert.IsLessThanOrEqualTo(tolerance, MaxChannelDistance(expected, hex), $"{hex} vs rgb({string.Join(", ", expected)})");

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
    /// Writes an untagged sRGB JPEG whose left and right halves have the given colours.
    /// </summary>
    private static void WriteHalvesJpeg(string path, double[] left, double[] right)
    {
        using var xyz = Image.Xyz(800, 600);
        using var x = xyz[0];
        using var isLeft = x < 400;
        using var black = Image.Black(800, 600, bands: 3);
        using var leftImage = black + left;
        using var rightImage = black + right;
        using var joined = isLeft.Ifthenelse(leftImage, rightImage);
        using var pixels = joined.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        pixels.Jpegsave(path, q: 95);
    }

    /// <summary>
    /// Writes a red/green gradient (including vivid greens with little red) tagged with a libvips profile.
    /// </summary>
    private static void WriteGradientJpeg(string path, int width, int height, string profile)
    {
        using var xyz = Image.Xyz(width, height);
        using var x = xyz[0];
        using var y = xyz[1];
        using var red = x * (255.0 / width);
        using var green = y * (255.0 / height);
        using var blue = (x + y) * (64.0 / (width + height));
        using var joined = red.Bandjoin(green, blue);
        using var pixels = joined.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        using var tagged = pixels.IccTransform(profile, inputProfile: "srgb", intent: Enums.Intent.Relative);
        tagged.Jpegsave(path, q: 95, keep: Enums.ForeignKeep.All);
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
