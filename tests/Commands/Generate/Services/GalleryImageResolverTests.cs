using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Configuration;
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
            "exif.make == 'Canon' | sort dateTaken desc | limit 2",
            wideGamut: true);

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
        var result = GalleryImageResolver.Resolve(images, "all | sort dateTaken asc", wideGamut: true);

        // Assert
        Assert.AreEqual("dated.jpg", result[0].SourcePath);
        Assert.AreEqual("undated.jpg", result[1].SourcePath);
    }

    [TestMethod]
    public void Resolve_LimitWithoutPipeSort_HonorsPageSortBeforeLimit()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["first.jpg"] = CreateImage("first.jpg", "Canon", new DateTime(2024, 1, 1)),
            ["second.jpg"] = CreateImage("second.jpg", "Canon", new DateTime(2026, 1, 1)),
            ["third.jpg"] = CreateImage("third.jpg", "Canon", new DateTime(2025, 1, 1))
        };
        var globalSort = new ImageSortConfig
        {
            Field = "filename",
            Direction = SortDirection.Asc,
            Fallback = "filename"
        };

        // Act
        var result = GalleryImageResolver.Resolve(images, "all | limit 2", wideGamut: true, "dateTaken:desc", globalSort);

        // Assert
        Assert.AreEqual("second.jpg", result[0].SourcePath);
        Assert.AreEqual("third.jpg", result[1].SourcePath);
    }

    [TestMethod]
    public void Resolve_ScannedDescriptiveMetadata_PopulatesTemplateFields()
    {
        var images = new Dictionary<string, ImageContent>
        {
            ["described.jpg"] = new ImageContent
            {
                Filename = "described.jpg",
                Width = 1920,
                Height = 1080,
                Sizes = [320, 640, 1280],
                Title = "Abendlicht",
                Description = "Evening light",
                Keywords = ["Selected", "Startseite"],
                Rating = 4
            }
        };

        var result = GalleryImageResolver.Resolve(images, "all", wideGamut: true);

        Assert.HasCount(1, result);
        Assert.AreEqual("Abendlicht", result[0].Title);
        Assert.AreEqual("Evening light", result[0].Description);
        Assert.AreEqual("Selected,Startseite", string.Join(',', result[0].Keywords));
        Assert.AreEqual(4, result[0].Rating);
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
