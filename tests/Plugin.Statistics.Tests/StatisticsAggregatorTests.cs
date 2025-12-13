using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Manifest;
using Spectara.Revela.Plugin.Statistics.Configuration;
using Spectara.Revela.Plugin.Statistics.Services;

namespace Spectara.Revela.Plugin.Statistics.Tests;

[TestClass]
public sealed class StatisticsAggregatorTests
{
    private IManifestRepository manifestRepository = null!;
    private IOptionsMonitor<StatisticsPluginConfig> config = null!;
    private ILogger logger = null!;

    [TestInitialize]
    public void Setup()
    {
        manifestRepository = Substitute.For<IManifestRepository>();
        config = Substitute.For<IOptionsMonitor<StatisticsPluginConfig>>();
        config.CurrentValue.Returns(new StatisticsPluginConfig());
        logger = Substitute.For<ILogger>();
    }

    [TestMethod]
    public void Aggregate_EmptyManifest_ReturnsZeroCounts()
    {
        // Arrange
        manifestRepository.Images.Returns(new Dictionary<string, ImageContent>());
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.AreEqual(0, result.TotalImages);
        Assert.AreEqual(0, result.ImagesWithExif);
        Assert.IsEmpty(result.Apertures);
        Assert.IsEmpty(result.FocalLengths);
    }

    [TestMethod]
    public void Aggregate_CountsTotalImages()
    {
        // Arrange
        var images = CreateTestImages(10);
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.AreEqual(10, result.TotalImages);
    }

    [TestMethod]
    public void Aggregate_CountsImagesWithExif()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = CreateImage("img1.jpg", new ExifData { FNumber = 2.8 }),
            ["img2.jpg"] = CreateImage("img2.jpg", new ExifData { Iso = 100 }),
            ["img3.jpg"] = CreateImage("img3.jpg", exif: null) // No EXIF
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.AreEqual(3, result.TotalImages);
        Assert.AreEqual(2, result.ImagesWithExif);
    }

    [TestMethod]
    public void Aggregate_BucketsApertures()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = CreateImage("img1.jpg", new ExifData { FNumber = 1.4 }),
            ["img2.jpg"] = CreateImage("img2.jpg", new ExifData { FNumber = 1.8 }),
            ["img3.jpg"] = CreateImage("img3.jpg", new ExifData { FNumber = 2.8 }),
            ["img4.jpg"] = CreateImage("img4.jpg", new ExifData { FNumber = 4.0 }) // Falls into f/2.8-4.0 bucket
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.HasCount(3, result.Apertures); // 3 different buckets
        var bucket14 = result.Apertures.FirstOrDefault(a => a.Label == "f/1.4-2.0");
        Assert.IsNotNull(bucket14);
        Assert.AreEqual(2, bucket14.Count); // 1.4 and 1.8 both fall in 1.4-2.0
    }

    [TestMethod]
    public void Aggregate_BucketsFocalLengths()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = CreateImage("img1.jpg", new ExifData { FocalLength = 24 }),  // 18-35mm
            ["img2.jpg"] = CreateImage("img2.jpg", new ExifData { FocalLength = 50 }),  // 35-70mm
            ["img3.jpg"] = CreateImage("img3.jpg", new ExifData { FocalLength = 85 }),  // 70-135mm
            ["img4.jpg"] = CreateImage("img4.jpg", new ExifData { FocalLength = 200 })  // 135-300mm
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.HasCount(4, result.FocalLengths);
        Assert.IsTrue(result.FocalLengths.Any(f => f.Label == "18-35mm"));
        Assert.IsTrue(result.FocalLengths.Any(f => f.Label == "35-70mm"));
        Assert.IsTrue(result.FocalLengths.Any(f => f.Label == "70-135mm"));
        Assert.IsTrue(result.FocalLengths.Any(f => f.Label == "135-300mm"));
    }

    [TestMethod]
    public void Aggregate_SortsByFrequencyWhenConfigured()
    {
        // Arrange
        config.CurrentValue.Returns(new StatisticsPluginConfig { SortByCount = true });
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = CreateImage("img1.jpg", new ExifData { FNumber = 1.4 }),  // f/1.4-2.0 bucket
            ["img2.jpg"] = CreateImage("img2.jpg", new ExifData { FNumber = 2.8 }),  // f/2.8-4.0 bucket
            ["img3.jpg"] = CreateImage("img3.jpg", new ExifData { FNumber = 2.8 }),  // f/2.8-4.0 bucket
            ["img4.jpg"] = CreateImage("img4.jpg", new ExifData { FNumber = 2.8 })   // f/2.8-4.0 bucket
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert - f/2.8-4.0 should be first (3 occurrences vs 1)
        Assert.AreEqual("f/2.8-4.0", result.Apertures[0].Label);
        Assert.AreEqual(3, result.Apertures[0].Count);
    }

    [TestMethod]
    public void Aggregate_LimitsEntriesPerCategory()
    {
        // Arrange
        config.CurrentValue.Returns(new StatisticsPluginConfig { MaxEntriesPerCategory = 2 });
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = CreateImage("img1.jpg", new ExifData { Model = "Camera A" }),
            ["img2.jpg"] = CreateImage("img2.jpg", new ExifData { Model = "Camera A" }),
            ["img3.jpg"] = CreateImage("img3.jpg", new ExifData { Model = "Camera B" }),
            ["img4.jpg"] = CreateImage("img4.jpg", new ExifData { Model = "Camera C" })
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert - Should have 2 cameras + "Other"
        Assert.HasCount(3, result.CameraModels);
        Assert.IsTrue(result.CameraModels.Any(c => c.Label == "Other"));
    }

    [TestMethod]
    public void Aggregate_CalculatesPercentCorrectly()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = CreateImage("img1.jpg", new ExifData { Model = "A" }),
            ["img2.jpg"] = CreateImage("img2.jpg", new ExifData { Model = "A" }),
            ["img3.jpg"] = CreateImage("img3.jpg", new ExifData { Model = "A" }),
            ["img4.jpg"] = CreateImage("img4.jpg", new ExifData { Model = "A" }),
            ["img5.jpg"] = CreateImage("img5.jpg", new ExifData { Model = "B" }),
            ["img6.jpg"] = CreateImage("img6.jpg", new ExifData { Model = "B" })
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert - A has 4 (100%), B has 2 (50%)
        var cameraA = result.CameraModels.First(c => c.Label == "A");
        var cameraB = result.CameraModels.First(c => c.Label == "B");
        Assert.AreEqual(100, cameraA.Percent);
        Assert.AreEqual(50, cameraB.Percent);
    }

    [TestMethod]
    public void Aggregate_GroupsByYear()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = CreateImage("img1.jpg", new ExifData(), dateTaken: new DateTime(2024, 6, 15)),
            ["img2.jpg"] = CreateImage("img2.jpg", new ExifData(), dateTaken: new DateTime(2024, 3, 10)),
            ["img3.jpg"] = CreateImage("img3.jpg", new ExifData(), dateTaken: new DateTime(2023, 12, 1)),
            ["img4.jpg"] = CreateImage("img4.jpg", new ExifData(), dateTaken: new DateTime(2023, 1, 1))
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.HasCount(2, result.ImagesByYear);
        Assert.IsTrue(result.ImagesByYear.Any(y => y.Label == "2024" && y.Count == 2));
        Assert.IsTrue(result.ImagesByYear.Any(y => y.Label == "2023" && y.Count == 2));
        // Most recent year should be first
        Assert.AreEqual("2024", result.ImagesByYear[0].Label);
    }

    #region Helper Methods

    private static Dictionary<string, ImageContent> CreateTestImages(int count)
    {
        var images = new Dictionary<string, ImageContent>();
        for (var i = 0; i < count; i++)
        {
            var path = $"img{i}.jpg";
            images[path] = CreateImage(path, new ExifData { FNumber = 2.8, Iso = 100 });
        }
        return images;
    }

    private static ImageContent CreateImage(string filename, ExifData? exif, DateTime? dateTaken = null) => new()
    {
        Filename = filename,
        Hash = $"hash_{filename}",
        Width = 1920,
        Height = 1080,
        Sizes = [1920],
        Exif = exif,
        DateTaken = dateTaken
    };

    #endregion
}
