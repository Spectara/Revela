using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetVips;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Abstractions;
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
/// End-to-end tests for wide-gamut (Display P3) output.
/// </summary>
/// <remarks>
/// Photos whose colours sRGB cannot show are published in Display P3 with the profile embedded;
/// every other photo stays exactly as before (untagged sRGB). <c>generate.images.wideGamut</c>
/// switches P3 output off.
/// </remarks>
[TestClass]
[TestCategory("E2E")]
public sealed class WideGamutTests
{
    private const string GalleryName = "Photos";
    private const string GallerySlug = "photos";
    private const int VariantSize = 640;
    private const string ProfileField = TestIccProfiles.ProfileField;

    [TestMethod]
    public async Task ScanAsync_SourcesInSeveralColorSpaces_DetectsVisibleOutOfSrgbContent()
    {
        using var project = CreateProject(
            ["untagged.jpg", "srgb.jpg", "p3-vivid.jpg", "p3-in-srgb-gamut.jpg", "adobe-vivid.jpg", "adobe-in-srgb-gamut.jpg"]);
        var adobe = TestIccProfiles.AdobeRgbCompatible();
        var adobePath = WriteProfile(project, adobe);
        using (var gradient = SrgbGradient(800, 600))
        {
            Save(gradient, SourceFile(project, "untagged.jpg"), profile: null);
            using var srgb = gradient.IccTransform("srgb", inputProfile: "srgb", intent: Enums.Intent.Relative);
            Save(srgb, SourceFile(project, "srgb.jpg"));
        }

        // The most saturated sRGB colours: inside sRGB, but JPEG noise in a wider colour space
        // pushes single pixels outside it (up to ΔE00 4), which must not make a P3 photo.
        using (var tiles = SrgbPrimaryTiles(800, 600))
        {
            using var p3 = tiles.IccTransform("p3", inputProfile: "srgb", intent: Enums.Intent.Relative);
            Save(p3, SourceFile(project, "p3-in-srgb-gamut.jpg"), quality: 80);
            using var adobeRgb = tiles.IccTransform(adobePath, inputProfile: "srgb", intent: Enums.Intent.Relative);
            Save(adobeRgb, SourceFile(project, "adobe-in-srgb-gamut.jpg"), quality: 80);
        }

        WriteVivid(SourceFile(project, "p3-vivid.jpg"), TestIccProfiles.BuiltIn("p3"));
        WriteVivid(SourceFile(project, "adobe-vivid.jpg"), adobe);

        var gamuts = await ScanAsync(project, image => image.Gamut);

        Assert.AreEqual("srgb", gamuts["untagged.jpg"]);
        Assert.AreEqual("srgb", gamuts["srgb.jpg"]);
        Assert.AreEqual("p3", gamuts["p3-vivid.jpg"]);
        Assert.AreEqual("srgb", gamuts["p3-in-srgb-gamut.jpg"], "A Display P3 profile alone does not make a P3 photo.");
        Assert.AreEqual("p3", gamuts["adobe-vivid.jpg"]);
        Assert.AreEqual("srgb", gamuts["adobe-in-srgb-gamut.jpg"]);
    }

    [TestMethod]
    public async Task ScanAsync_VividP3Source_ColorIsSrgbHexOfTheConvertedColor()
    {
        // The placeholder is painted as CSS hex (sRGB), so it is computed after the conversion to sRGB.
        using var project = CreateProject(["solid.jpg"]);
        double[] p3Red = [255, 0, 0];
        using (var solid = Solid(p3Red, 800, 600))
        {
            Save(solid, SourceFile(project, "solid.jpg"), TestIccProfiles.BuiltIn("p3"));
        }

        var colors = await ScanAsync(project, image => image.Color);

        using var pixel = Solid(p3Red, 1, 1);
        using var reference = pixel.Mutate(m => m.Set(GValue.BlobType, ProfileField, TestIccProfiles.BuiltIn("p3")));
        using var srgb = reference.IccTransform("srgb", embedded: true, intent: Enums.Intent.Perceptual);
        var expected = srgb.Getpoint(0, 0);
        var hex = colors["solid.jpg"]!;
        Assert.MatchesRegex("^#[0-9a-f]{6}$", hex);
        for (var band = 0; band < 3; band++)
        {
            var actual = Convert.ToInt32(hex.Substring(1 + (band * 2), 2), 16);
            Assert.IsLessThanOrEqualTo(3, Math.Abs(actual - expected[band]), $"{hex} vs rgb({string.Join(", ", expected)})");
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WideGamutSource_EveryFormatEmbedsOnlyTheDisplayP3Profile()
    {
        using var project = CreateProject(["vivid.jpg", "plain.jpg"]);
        WriteVivid(SourceFile(project, "vivid.jpg"), TestIccProfiles.BuiltIn("p3"), withXmp: true);
        using (var gradient = SrgbGradient(1280, 960))
        {
            Save(gradient, SourceFile(project, "plain.jpg"), TestIccProfiles.BuiltIn("srgb"));
        }

        await RunAsync(project);

        var p3Profile = TestIccProfiles.BuiltIn("p3");
        Assert.IsLessThanOrEqualTo(600, p3Profile.Length, "The embedded profile costs at most 600 bytes per file.");
        var vivid = Variants(project, "vivid");
        Assert.HasCount(6, vivid, "640 and 1280 as AVIF, WebP and JPG.");
        foreach (var variant in vivid)
        {
            using var image = Image.NewFromFile(variant);
            Assert.IsTrue(image.Contains(ProfileField), $"{Path.GetFileName(variant)} must embed the Display P3 profile.");
            CollectionAssert.AreEqual(p3Profile, (byte[])image.Get(ProfileField), Path.GetFileName(variant));
            Assert.IsFalse(image.Contains("xmp-data"), $"{Path.GetFileName(variant)} must not keep XMP.");
            Assert.IsFalse(image.Contains("exif-data"), $"{Path.GetFileName(variant)} must not keep EXIF.");
        }

        foreach (var variant in Variants(project, "plain"))
        {
            using var image = Image.NewFromFile(variant);
            Assert.IsFalse(image.Contains(ProfileField), $"{Path.GetFileName(variant)} is sRGB and stays untagged.");
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WideGamutSource_VariantsMatchSourceColorsWithinDeltaE1()
    {
        using var project = CreateProject(["vivid.jpg"]);
        WriteVivid(SourceFile(project, "vivid.jpg"), TestIccProfiles.BuiltIn("p3"));

        await RunAsync(project);

        using var source = Image.NewFromFile(SourceFile(project, "vivid.jpg"));
        using var resized = source.ThumbnailImage(VariantSize);
        using var expected = ToLab(resized);
        foreach (var variant in Variants(project, "vivid").Where(v => Path.GetFileNameWithoutExtension(v) == "640"))
        {
            using var image = Image.NewFromFile(variant);
            using var actual = ToLab(image);
            using var difference = expected.DE00(actual);
            Assert.IsLessThan(1d, difference.Avg(), $"{Path.GetFileName(variant)}: mean ΔE00 to the source.");
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WideGamutDisabled_PublishesUntaggedSrgbLikeBefore()
    {
        using var project = CreateProject(["vivid.jpg"], wideGamut: false);
        var source = SourceFile(project, "vivid.jpg");
        WriteVivid(source, TestIccProfiles.BuiltIn("p3"));

        await RunAsync(project);

        var variants = Variants(project, "vivid");
        Assert.HasCount(6, variants);
        using var original = Image.NewFromFile(source);
        foreach (var variant in variants)
        {
            if (variant.EndsWith(".avif", StringComparison.Ordinal))
            {
                // AV1 encodes may differ with the thread count, so AVIF is checked for its tag only.
                using var avif = Image.NewFromFile(variant);
                Assert.IsFalse(avif.Contains(ProfileField), Path.GetFileName(variant));
                continue;
            }

            var width = int.Parse(Path.GetFileNameWithoutExtension(variant), System.Globalization.CultureInfo.InvariantCulture);
            using var resized = width >= original.Width ? original.Copy() : original.ThumbnailImage(width);
            using var converted = resized.IccTransform("srgb", embedded: true, intent: Enums.Intent.Perceptual);
            var expected = variant.EndsWith(".webp", StringComparison.Ordinal)
                ? converted.WebpsaveBuffer(q: 85, keep: Enums.ForeignKeep.None)
                : converted.JpegsaveBuffer(q: 90, keep: Enums.ForeignKeep.None);

            CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(variant), Path.GetFileName(variant));
        }

        Assert.IsEmpty(SocialCopies(project));
    }

    [TestMethod]
    public async Task ProcessAsync_WideGamutSource_WritesUntaggedSrgbSocialCopy()
    {
        using var project = CreateProject(["vivid.jpg"]);
        var source = SourceFile(project, "vivid.jpg");
        WriteVivid(source, TestIccProfiles.BuiltIn("p3"));

        await RunAsync(project);

        // The largest size up to 1920 px, like Lumina's og:image.
        var socialCopy = Path.Combine(ImageDirectory(project, "vivid"), "1280.srgb.jpg");
        CollectionAssert.AreEqual(new[] { socialCopy }, SocialCopies(project));
        using var original = Image.NewFromFile(source);
        using var converted = original.IccTransform("srgb", embedded: true, intent: Enums.Intent.Perceptual);
        var expected = converted.JpegsaveBuffer(q: 90, keep: Enums.ForeignKeep.None);
        CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(socialCopy), "Untagged sRGB at the JPG quality.");
    }

    [TestMethod]
    public async Task ProcessAsync_WideGamutToggled_ReencodesOnlyTheWideGamutPhoto()
    {
        using var project = CreateProject(["vivid.jpg", "plain.jpg"]);
        WriteVivid(SourceFile(project, "vivid.jpg"), TestIccProfiles.BuiltIn("p3"));
        using (var gradient = SrgbGradient(1280, 960))
        {
            Save(gradient, SourceFile(project, "plain.jpg"), TestIccProfiles.BuiltIn("srgb"));
        }

        await RunAsync(project);
        var plainWrites = Variants(project, "plain").ToDictionary(path => path, File.GetLastWriteTimeUtc, StringComparer.Ordinal);

        WriteProjectJson(project, wideGamut: false);
        var off = await RunAsync(project);

        Assert.AreEqual(1, off.ProcessedCount, "Only the P3 photo changes.");
        Assert.AreEqual(1, off.SkippedCount);
        foreach (var (path, written) in plainWrites)
        {
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(path), $"{Path.GetFileName(path)} must not be re-encoded.");
        }

        foreach (var variant in Variants(project, "vivid"))
        {
            using var image = Image.NewFromFile(variant);
            Assert.IsFalse(image.Contains(ProfileField), $"{Path.GetFileName(variant)} is sRGB with wideGamut off.");
        }

        Assert.IsEmpty(SocialCopies(project), "The social copy is removed with P3 output.");

        WriteProjectJson(project, wideGamut: true);
        var on = await RunAsync(project);

        Assert.AreEqual(1, on.ProcessedCount);
        Assert.HasCount(1, SocialCopies(project));
    }

    [TestMethod]
    public async Task ProcessAsync_SocialCopyDeleted_RewritesOnlyTheSocialCopy()
    {
        using var project = CreateProject(["vivid.jpg"]);
        WriteVivid(SourceFile(project, "vivid.jpg"), TestIccProfiles.BuiltIn("p3"));
        await RunAsync(project);
        var socialCopy = SocialCopies(project).Single();
        File.Delete(socialCopy);
        var avif = Variants(project, "vivid").First(v => v.EndsWith(".avif", StringComparison.Ordinal));
        var avifWritten = File.GetLastWriteTimeUtc(avif);

        var rerun = await RunAsync(project);

        Assert.AreEqual(1, rerun.ProcessedCount);
        Assert.IsTrue(File.Exists(socialCopy));
        Assert.AreEqual(avifWritten, File.GetLastWriteTimeUtc(avif), "Variants of other formats are kept.");
    }

    [TestMethod]
    public async Task RenderAsync_WideGamutPhoto_OpenGraphUsesTheSrgbCopy()
    {
        using var project = CreateProject(["vivid.jpg", "plain.jpg"], baseUrl: "https://photos.example.com");
        WriteVivid(SourceFile(project, "vivid.jpg"), TestIccProfiles.BuiltIn("p3"));
        using (var gradient = SrgbGradient(1280, 960))
        {
            Save(gradient, SourceFile(project, "plain.jpg"), TestIccProfiles.BuiltIn("srgb"));
        }

        using var host = BuildHost(project);
        await RunAsync(host);
        var render = await host.Services.GetRequiredService<IRenderService>().RenderAsync();
        Assert.IsTrue(render.Success, render.ErrorMessage);

        Assert.AreEqual(
            $"https://photos.example.com/images/{GallerySlug}/vivid/1280.srgb.jpg",
            OpenGraphImage(project, "vivid"));
        Assert.AreEqual(
            $"https://photos.example.com/images/{GallerySlug}/plain/1280.jpg",
            OpenGraphImage(project, "plain"));
        Assert.IsTrue(File.Exists(Path.Combine(ImageDirectory(project, "vivid"), "1280.srgb.jpg")));
    }

    private static TestProject CreateProject(string[] fileNames, bool wideGamut = true, string? baseUrl = null)
    {
        var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Gamut", author = "Test" })
            .AddGallery(GalleryName, g =>
            {
                foreach (var fileName in fileNames)
                {
                    g.AddRealImage(fileName, 800, 600);
                }
            }));
        WriteProjectJson(project, wideGamut, baseUrl);
        return project;
    }

    private static void WriteProjectJson(TestProject project, bool wideGamut, string? baseUrl = null)
    {
        var projectSection = new JsonObject { ["name"] = "Gamut" };
        if (baseUrl is not null)
        {
            projectSection["baseUrl"] = baseUrl;
        }

        var root = new JsonObject
        {
            ["project"] = projectSection,
            ["theme"] = new JsonObject { ["name"] = "Lumina" },
            ["generate"] = new JsonObject
            {
                ["images"] = new JsonObject { ["avif"] = 75, ["webp"] = 85, ["jpg"] = 90, ["wideGamut"] = wideGamut }
            },
        };
        File.WriteAllText(Path.Combine(project.RootPath, "project.json"), root.ToJsonString());
    }

    private static IHost BuildHost(TestProject project) =>
        RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<IImageSizesProvider>(new FixedSizesProvider([VariantSize]));
        });

    private static async Task<ImageResult> RunAsync(TestProject project)
    {
        using var host = BuildHost(project);
        return await RunAsync(host);
    }

    private static async Task<ImageResult> RunAsync(IHost host)
    {
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scan.Success, scan.ErrorMessage);

        var images = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(images.Success, images.ErrorMessage);
        return images;
    }

    private static async Task<Dictionary<string, string?>> ScanAsync(TestProject project, Func<Sdk.Models.Manifest.ImageContent, string?> select)
    {
        using var host = BuildHost(project);
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scan.Success, scan.ErrorMessage);

        return host.Services.GetRequiredService<IManifestRepository>().Images.Values
            .ToDictionary(i => i.Filename, select, StringComparer.Ordinal);
    }

    private static string SourceFile(TestProject project, string fileName) =>
        Path.Combine(project.SourcePath, GalleryName, fileName);

    private static string ImageDirectory(TestProject project, string name) =>
        Path.Combine(project.OutputPath, "images", GallerySlug, name);

    private static List<string> Variants(TestProject project, string name) => [.. Directory
        .EnumerateFiles(ImageDirectory(project, name))
        .Where(path => int.TryParse(Path.GetFileNameWithoutExtension(path), out _))
        .Order(StringComparer.Ordinal)];

    private static List<string> SocialCopies(TestProject project) => [.. Directory
        .EnumerateFiles(Path.Combine(project.OutputPath, "images"), "*.srgb.jpg", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)];

    private static string OpenGraphImage(TestProject project, string name)
    {
        const string prefix = "property=\"og:image\" content=\"";
        var html = File.ReadAllText(Path.Combine(project.OutputPath, "photo", GallerySlug, name, "index.html"));
        var start = html.IndexOf(prefix, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"{name}: no og:image.");
        start += prefix.Length;
        return html[start..html.IndexOf('"', start)];
    }

    private static string WriteProfile(TestProject project, byte[] profile)
    {
        var path = Path.Combine(project.RootPath, "adobe-rgb-compatible.icc");
        File.WriteAllBytes(path, profile);
        return path;
    }

    /// <summary>
    /// Converts an image through its embedded profile to CIE Lab (D65, libvips' Lab).
    /// </summary>
    private static Image ToLab(Image image)
    {
        using var xyz = image.IccImport(embedded: true, intent: Enums.Intent.Relative, pcs: Enums.PCS.Xyz);
        return xyz.Colourspace(Enums.Interpretation.Lab);
    }

    /// <summary>
    /// Saves 8-bit pixels as a JPEG tagged with <paramref name="profile"/> (untagged when <c>null</c>).
    /// </summary>
    private static void Save(Image pixels, string path, byte[]? profile = null, int quality = 95)
    {
        if (profile is null && !pixels.Contains(ProfileField))
        {
            pixels.Jpegsave(path, q: quality, keep: Enums.ForeignKeep.None);
            return;
        }

        using var tagged = profile is null ? pixels.Copy() : pixels.Mutate(m => m.Set(GValue.BlobType, ProfileField, profile));
        tagged.Jpegsave(path, q: quality, keep: Enums.ForeignKeep.All);
    }

    /// <summary>
    /// Writes a smooth sweep from the profile's pure red to its pure green (1280×960) — far
    /// outside sRGB for Display P3 and Adobe RGB.
    /// </summary>
    private static void WriteVivid(string path, byte[] profile, bool withXmp = false)
    {
        using var xyz = Image.Xyz(1280, 960);
        using var x = xyz[0];
        using var y = xyz[1];
        using var red = x * (255.0 / 1280);
        using var green = (x * (-255.0 / 1280)) + 255.0;
        using var blue = y * (64.0 / 960);
        using var joined = red.Bandjoin(green, blue);
        using var pixels = joined.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        if (!withXmp)
        {
            Save(pixels, path, profile);
            return;
        }

        var xmp = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"/></x:xmpmeta>"u8.ToArray();
        using var withMetadata = pixels.Mutate(m => m.Set(GValue.BlobType, "xmp-data", xmp));
        Save(withMetadata, path, profile);
        using var check = Image.NewFromFile(path);
        Assert.IsTrue(check.Contains("xmp-data"), "Fixture must carry XMP.");
    }

    /// <summary>
    /// An sRGB gradient whose colours all lie inside sRGB (including vivid greens with little red).
    /// </summary>
    private static Image SrgbGradient(int width, int height)
    {
        using var xyz = Image.Xyz(width, height);
        using var x = xyz[0];
        using var y = xyz[1];
        using var red = x * (255.0 / width);
        using var green = y * (255.0 / height);
        using var blue = (x + y) * (64.0 / (width + height));
        using var joined = red.Bandjoin(green, blue);
        return joined.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }

    private static Image Solid(double[] color, int width, int height)
    {
        using var black = Image.Black(width, height, bands: 3);
        using var colored = black + color;
        return colored.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }

    /// <summary>
    /// 100 px tiles of the sRGB primaries and secondaries (red, green, blue, yellow, cyan, magenta).
    /// </summary>
    private static Image SrgbPrimaryTiles(int width, int height)
    {
        double[][] colors = [[255, 0, 0], [0, 255, 0], [0, 0, 255], [255, 255, 0], [0, 255, 255], [255, 0, 255]];
        using var xyz = Image.Xyz(width, height);
        using var column = xyz[0] / 100;
        using var row = xyz[1] / 100;
        using var columnIndex = column.Cast(Enums.BandFormat.Int);
        using var rowIndex = row.Cast(Enums.BandFormat.Int);
        using var tileIndex = (columnIndex + (rowIndex * 8)) % colors.Length;
        var tiles = Solid(colors[^1], width, height);
        for (var i = colors.Length - 2; i >= 0; i--)
        {
            using var isTile = tileIndex.Equal(i);
            using var previous = tiles;
            tiles = isTile.Ifthenelse(colors[i], previous);
        }

        using var result = tiles;
        return result.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }

    private sealed class FixedSizesProvider(IReadOnlyList<int> sizes) : IImageSizesProvider
    {
        public IReadOnlyList<int> GetSizes() => sizes;

        public string GetResizeMode() => "longest";
    }
}
