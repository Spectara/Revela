using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Image = NetVips.Image;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// End-to-end regression tests for EXIF orientation handling (see #98).
/// </summary>
/// <remarks>
/// <para>
/// Runs the real scan → image-processing pipeline on JPEGs whose visual orientation lives
/// only in the EXIF Orientation tag. Verifies that scanned dimensions and every generated
/// variant — smallest, intermediate, and the direct largest-size path — are physically
/// upright, and that the published variants no longer carry any EXIF/XMP/GPS/orientation
/// metadata (privacy behaviour, <c>ForeignKeep.None</c>, must be preserved).
/// </para>
/// </remarks>
[TestClass]
[TestCategory("E2E")]
public sealed class OrientationRegressionTests
{
    private const string GalleryName = "Photos";

    [TestMethod]
    [DataRow(3)]
    [DataRow(6)]
    [DataRow(8)]
    public async Task Pipeline_OrientedImage_ProducesUprightVariantsWithStrippedMetadata(int orientation)
    {
        // Arrange: a single asymmetric image stored sideways/upside-down with an EXIF
        // Orientation tag. Stored as landscape 1600x1000 so 6/8 swap to portrait upright.
        var fileName = $"rotated{orientation}.jpg";
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Orientation" },
                theme = new { name = "Lumina" },
                generate = new { images = new { jpg = 90 } }
            })
            .WithSiteJson(new { title = "Orientation", author = "Test" })
            .AddGallery(GalleryName, g => g.AddOrientedImage(fileName, orientation)));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<IImageSizesProvider>(new FixedSizesProvider([320, 640]));
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var imageService = host.Services.GetRequiredService<IImageService>();
        var manifest = host.Services.GetRequiredService<IManifestRepository>();

        // Independent oracle: the upright reference derived straight from the source file.
        var sourceFile = Path.Combine(project.SourcePath, GalleryName, fileName);
        using var raw = Image.NewFromFile(sourceFile);
        using var uprightReference = raw.Autorot();
        var uprightWidth = uprightReference.Width;
        var uprightHeight = uprightReference.Height;
        var uprightCorner = ReddestCorner(uprightReference);

        // Guard: the fixture must actually require rotation, otherwise the test proves nothing.
        Assert.AreNotEqual(
            ReddestCorner(raw),
            uprightCorner,
            "Fixture must store non-upright pixels so orientation handling is exercised.");

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");
        Assert.AreEqual(1, scanResult.ImageCount, "Exactly one image should be scanned");

        // Scanned dimensions must describe the UPRIGHT image (6/8 swap width/height).
        var scanned = manifest.Images.Values.Single();
        Assert.AreEqual(uprightWidth, scanned.Width, "Scanned width must be the upright width");
        Assert.AreEqual(uprightHeight, scanned.Height, "Scanned height must be the upright height");

        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(imageResult.Success, $"Image processing should succeed: {imageResult.ErrorMessage}");

        // Assert: three JPEG variants — smallest, intermediate, and the original (upright) size.
        var imagesDir = Path.Combine(project.OutputPath, "images");
        var variants = Directory
            .EnumerateFiles(imagesDir, "*.jpg", SearchOption.AllDirectories)
            .OrderBy(VariantWidth)
            .ToList();
        Assert.HasCount(3, variants);

        var smallest = variants[0];
        var intermediate = variants[1];
        var largest = variants[^1];

        // The original-size variant filename encodes the upright width.
        Assert.AreEqual(uprightWidth, VariantWidth(largest), "Largest variant must be the upright width");

        // Every variant must be physically upright: correct dimensions and marker corner.
        using var largestImage = Image.NewFromFile(largest);
        Assert.AreEqual(uprightWidth, largestImage.Width, "Original-size variant width must be upright");
        Assert.AreEqual(uprightHeight, largestImage.Height, "Original-size variant height must be upright");
        Assert.AreEqual(uprightCorner, ReddestCorner(largestImage), "Original-size variant must be upright");

        using var smallestImage = Image.NewFromFile(smallest);
        Assert.AreEqual(uprightCorner, ReddestCorner(smallestImage), "Smallest variant must be upright");

        using var intermediateImage = Image.NewFromFile(intermediate);
        Assert.AreEqual(uprightCorner, ReddestCorner(intermediateImage), "Intermediate variant must be upright");

        // Privacy guard: the original-size variant no longer relies on an orientation tag and
        // carries no EXIF/XMP/GPS/IPTC metadata (ForeignKeep.None must stay in effect).
        var strippedFields = largestImage.GetFields();
        Assert.IsFalse(
            strippedFields.Any(IsSensitiveMetadataField),
            $"Published variant must be stripped of EXIF/XMP/GPS/orientation metadata; found: {string.Join(", ", strippedFields)}");
    }

    private static bool IsSensitiveMetadataField(string field) =>
        field.Equals("orientation", StringComparison.Ordinal)
        || field.StartsWith("exif", StringComparison.Ordinal)
        || field.StartsWith("xmp", StringComparison.Ordinal)
        || field.StartsWith("gps", StringComparison.Ordinal)
        || field.StartsWith("iptc", StringComparison.Ordinal);

    private static int VariantWidth(string variantPath) =>
        int.Parse(Path.GetFileNameWithoutExtension(variantPath), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns the corner whose region is most dominated by the red marker.
    /// </summary>
    private static Corner ReddestCorner(Image image)
    {
        var cropWidth = Math.Max(1, image.Width / 4);
        var cropHeight = Math.Max(1, image.Height / 4);

        (Corner Corner, double Score)[] corners =
        [
            (Corner.TopLeft, Redness(image, 0, 0, cropWidth, cropHeight)),
            (Corner.TopRight, Redness(image, image.Width - cropWidth, 0, cropWidth, cropHeight)),
            (Corner.BottomLeft, Redness(image, 0, image.Height - cropHeight, cropWidth, cropHeight)),
            (Corner.BottomRight, Redness(image, image.Width - cropWidth, image.Height - cropHeight, cropWidth, cropHeight)),
        ];

        return corners.MaxBy(c => c.Score).Corner;
    }

    /// <summary>
    /// Mean "redness" (R minus the average of G and B) over a corner region.
    /// </summary>
    private static double Redness(Image image, int left, int top, int width, int height)
    {
        using var region = image.Crop(left, top, width, height);
        using var red = region[0];
        using var green = region[1];
        using var blue = region[2];
        return red.Avg() - ((green.Avg() + blue.Avg()) / 2.0);
    }

    private enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    private sealed class FixedSizesProvider(IReadOnlyList<int> sizes) : IImageSizesProvider
    {
        public IReadOnlyList<int> GetSizes() => sizes;

        public string GetResizeMode() => "longest";
    }
}
