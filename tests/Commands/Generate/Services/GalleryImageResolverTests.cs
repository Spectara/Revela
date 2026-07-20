using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// Tests for <see cref="GalleryImageResolver"/>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class GalleryImageResolverTests
{
    [TestMethod]
    public void Resolve_FilterSortAndLimit_ReturnsOrderedRenderImages()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["events/first.jpg"] = CreateImage("first.jpg", "Canon", new DateTime(2024, 1, 1)),
            ["events/second.jpg"] = CreateImage("second.jpg", "Sony", new DateTime(2025, 1, 1)),
            ["portraits/third.jpg"] = CreateImage("third.jpg", "Canon", new DateTime(2026, 1, 1)),
            ["portraits/fourth.jpg"] = CreateImage("fourth.jpg", "Canon", new DateTime(2025, 1, 1))
        };

        // Act
        var result = GalleryImageResolver.Resolve(
            images,
            "exif.make == 'Canon' | sort dateTaken desc | limit 2");

        // Assert
        Assert.HasCount(2, result);
        Assert.AreEqual("portraits/third.jpg", result[0].SourcePath);
        Assert.AreEqual("portraits/fourth.jpg", result[1].SourcePath);
        Assert.AreEqual("portraits/third", result[0].Slug);
    }

    [TestMethod]
    public void Resolve_NullDateTaken_SortsUsingManifestNullSemantics()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["dated.jpg"] = CreateImage("dated.jpg", "Canon", new DateTime(2024, 1, 1)),
            ["undated.jpg"] = CreateImage("undated.jpg", "Canon", null)
        };

        // Act
        var result = GalleryImageResolver.Resolve(images, "all | sort dateTaken asc");

        // Assert
        Assert.AreEqual("dated.jpg", result[0].SourcePath);
        Assert.AreEqual("undated.jpg", result[1].SourcePath);
    }

    private static ImageContent CreateImage(string filename, string make, DateTime? dateTaken) => new()
    {
        Filename = filename,
        Width = 1920,
        Height = 1080,
        Sizes = [320, 640, 1280],
        DateTaken = dateTaken,
        Exif = new ExifData { Make = make }
    };
}
