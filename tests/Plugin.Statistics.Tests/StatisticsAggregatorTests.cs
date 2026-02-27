using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Plugin.Statistics.Configuration;
using Spectara.Revela.Plugin.Statistics.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Tests.Shared;

namespace Spectara.Revela.Plugin.Statistics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class StatisticsAggregatorTests
{
    private readonly IManifestRepository manifestRepository;
    private readonly IOptionsMonitor<StatisticsPluginConfig> config;
    private readonly ILogger<StatisticsAggregator> logger;

    public StatisticsAggregatorTests()
    {
        manifestRepository = Substitute.For<IManifestRepository>();
        config = Substitute.For<IOptionsMonitor<StatisticsPluginConfig>>();
        config.CurrentValue.Returns(new StatisticsPluginConfig());
        logger = Substitute.For<ILogger<StatisticsAggregator>>();
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
        var images = TestData.Images(10);
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
            ["img1.jpg"] = TestData.Image("img1.jpg", TestData.Exif(fNumber: 2.8)),
            ["img2.jpg"] = TestData.Image("img2.jpg", TestData.Exif(iso: 100)),
            ["img3.jpg"] = TestData.Image("img3.jpg", exif: null) // No EXIF
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
            ["img1.jpg"] = TestData.Image("img1.jpg", TestData.Exif(fNumber: 1.4)),
            ["img2.jpg"] = TestData.Image("img2.jpg", TestData.Exif(fNumber: 1.8)),
            ["img3.jpg"] = TestData.Image("img3.jpg", TestData.Exif(fNumber: 2.8)),
            ["img4.jpg"] = TestData.Image("img4.jpg", TestData.Exif(fNumber: 4.0)) // Falls into f/2.8-4.0 bucket
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.HasCount(3, result.Apertures); // 3 different buckets
        var bucket14 = result.Apertures.FirstOrDefault(a => a.Name == "f/1.4-2.0");
        Assert.IsNotNull(bucket14);
        Assert.AreEqual(2, bucket14.Count); // 1.4 and 1.8 both fall in 1.4-2.0
    }

    [TestMethod]
    public void Aggregate_BucketsFocalLengths()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = TestData.Image("img1.jpg", TestData.Exif(focalLength: 24)),  // 18-35mm
            ["img2.jpg"] = TestData.Image("img2.jpg", TestData.Exif(focalLength: 50)),  // 35-70mm
            ["img3.jpg"] = TestData.Image("img3.jpg", TestData.Exif(focalLength: 85)),  // 70-135mm
            ["img4.jpg"] = TestData.Image("img4.jpg", TestData.Exif(focalLength: 200))  // 135-300mm
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.HasCount(4, result.FocalLengths);
        Assert.IsTrue(result.FocalLengths.Any(f => f.Name == "18\u201335"));
        Assert.IsTrue(result.FocalLengths.Any(f => f.Name == "35\u201370"));
        Assert.IsTrue(result.FocalLengths.Any(f => f.Name == "70\u2013135"));
        Assert.IsTrue(result.FocalLengths.Any(f => f.Name == "135\u2013300"));
    }

    [TestMethod]
    public void Aggregate_SortsByFrequencyWhenConfigured()
    {
        // Arrange
        config.CurrentValue.Returns(new StatisticsPluginConfig { SortByCount = true });
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = TestData.Image("img1.jpg", TestData.Exif(fNumber: 1.4)),  // f/1.4-2.0 bucket
            ["img2.jpg"] = TestData.Image("img2.jpg", TestData.Exif(fNumber: 2.8)),  // f/2.8-4.0 bucket
            ["img3.jpg"] = TestData.Image("img3.jpg", TestData.Exif(fNumber: 2.8)),  // f/2.8-4.0 bucket
            ["img4.jpg"] = TestData.Image("img4.jpg", TestData.Exif(fNumber: 2.8))   // f/2.8-4.0 bucket
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert - f/2.8-4.0 should be first (3 occurrences vs 1)
        Assert.AreEqual("f/2.8-4.0", result.Apertures[0].Name);
        Assert.AreEqual(3, result.Apertures[0].Count);
    }

    [TestMethod]
    public void Aggregate_LimitsEntriesPerCategory()
    {
        // Arrange
        config.CurrentValue.Returns(new StatisticsPluginConfig { MaxEntriesPerCategory = 2 });
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = TestData.Image("img1.jpg", TestData.Exif(model: "Camera A")),
            ["img2.jpg"] = TestData.Image("img2.jpg", TestData.Exif(model: "Camera A")),
            ["img3.jpg"] = TestData.Image("img3.jpg", TestData.Exif(model: "Camera B")),
            ["img4.jpg"] = TestData.Image("img4.jpg", TestData.Exif(model: "Camera C"))
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert - Should have 2 cameras + "Other"
        Assert.HasCount(3, result.Cameras);
        Assert.IsTrue(result.Cameras.Any(c => c.Name == "Other"));
    }

    [TestMethod]
    public void Aggregate_CalculatesPercentCorrectly()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = TestData.Image("img1.jpg", TestData.Exif(model: "A")),
            ["img2.jpg"] = TestData.Image("img2.jpg", TestData.Exif(model: "A")),
            ["img3.jpg"] = TestData.Image("img3.jpg", TestData.Exif(model: "A")),
            ["img4.jpg"] = TestData.Image("img4.jpg", TestData.Exif(model: "A")),
            ["img5.jpg"] = TestData.Image("img5.jpg", TestData.Exif(model: "B")),
            ["img6.jpg"] = TestData.Image("img6.jpg", TestData.Exif(model: "B"))
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert - A has 4 (100%), B has 2 (50%)
        var cameraA = result.Cameras.First(c => c.Name == "A");
        var cameraB = result.Cameras.First(c => c.Name == "B");
        Assert.AreEqual(100, cameraA.Percentage);
        Assert.AreEqual(50, cameraB.Percentage);
    }

    [TestMethod]
    public void Aggregate_GroupsByYear()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = TestData.Image("img1.jpg", TestData.Exif(), dateTaken: new DateTime(2024, 6, 15)),
            ["img2.jpg"] = TestData.Image("img2.jpg", TestData.Exif(), dateTaken: new DateTime(2024, 3, 10)),
            ["img3.jpg"] = TestData.Image("img3.jpg", TestData.Exif(), dateTaken: new DateTime(2023, 12, 1)),
            ["img4.jpg"] = TestData.Image("img4.jpg", TestData.Exif(), dateTaken: new DateTime(2023, 1, 1))
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.HasCount(2, result.ImagesByYear);
        Assert.IsTrue(result.ImagesByYear.Any(y => y.Name == "2024" && y.Count == 2));
        Assert.IsTrue(result.ImagesByYear.Any(y => y.Name == "2023" && y.Count == 2));
        // Most recent year should be first
        Assert.AreEqual("2024", result.ImagesByYear[0].Name);
    }

    [TestMethod]
    public void Aggregate_GroupsByMonth()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["img1.jpg"] = TestData.Image("img1.jpg", TestData.Exif(), dateTaken: new DateTime(2024, 1, 10)),
            ["img2.jpg"] = TestData.Image("img2.jpg", TestData.Exif(), dateTaken: new DateTime(2024, 1, 20)),
            ["img3.jpg"] = TestData.Image("img3.jpg", TestData.Exif(), dateTaken: new DateTime(2023, 7, 15)),
            ["img4.jpg"] = TestData.Image("img4.jpg", TestData.Exif(), dateTaken: new DateTime(2024, 7, 1))
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert - January: 2, July: 2
        Assert.HasCount(2, result.ImagesByMonth);
        Assert.IsTrue(result.ImagesByMonth.Any(m => m.Name == "January" && m.Count == 2));
        Assert.IsTrue(result.ImagesByMonth.Any(m => m.Name == "July" && m.Count == 2));
    }

    [TestMethod]
    public void Aggregate_DetectsOrientation()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["landscape.jpg"] = TestData.Image("landscape.jpg", TestData.Exif(), width: 1920, height: 1080),
            ["portrait.jpg"] = TestData.Image("portrait.jpg", TestData.Exif(), width: 1080, height: 1920),
            ["square.jpg"] = TestData.Image("square.jpg", TestData.Exif(), width: 1000, height: 1000)
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.HasCount(3, result.Orientations);
        Assert.IsTrue(result.Orientations.Any(o => o.Name == "Landscape" && o.Count == 1));
        Assert.IsTrue(result.Orientations.Any(o => o.Name == "Portrait" && o.Count == 1));
        Assert.IsTrue(result.Orientations.Any(o => o.Name == "Square" && o.Count == 1));
    }

    [TestMethod]
    public void Aggregate_SkipsZeroDimensionImages_ForOrientation()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["normal.jpg"] = TestData.Image("normal.jpg", TestData.Exif(), width: 1920, height: 1080),
            ["nodim.jpg"] = TestData.Image("nodim.jpg", TestData.Exif(), width: 0, height: 0)
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert - only the normal image should be counted
        Assert.HasCount(1, result.Orientations);
        Assert.AreEqual("Landscape", result.Orientations[0].Name);
    }

    [TestMethod]
    public void Aggregate_FormatsShutterSpeeds()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["fast.jpg"] = TestData.Image("fast.jpg", TestData.Exif(exposureTime: 1.0 / 500)),
            ["fast2.jpg"] = TestData.Image("fast2.jpg", TestData.Exif(exposureTime: 1.0 / 500)),
            ["slow.jpg"] = TestData.Image("slow.jpg", TestData.Exif(exposureTime: 2.0))
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.HasCount(2, result.ShutterSpeeds);
        Assert.IsTrue(result.ShutterSpeeds.Any(s => s.Name == "1/500" && s.Count == 2));
        Assert.IsTrue(result.ShutterSpeeds.Any(s => s.Name == "2s" && s.Count == 1));
    }

    [TestMethod]
    public void Aggregate_PhotoHeatmap_GroupsByYearAndMonth()
    {
        // Arrange
        var images = new Dictionary<string, ImageContent>
        {
            ["jan1.jpg"] = TestData.Image("jan1.jpg", dateTaken: new DateTime(2024, 1, 10)),
            ["jan2.jpg"] = TestData.Image("jan2.jpg", dateTaken: new DateTime(2024, 1, 20)),
            ["mar1.jpg"] = TestData.Image("mar1.jpg", dateTaken: new DateTime(2024, 3, 5)),
            ["old.jpg"] = TestData.Image("old.jpg", dateTaken: new DateTime(2023, 6, 15))
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert — 2 years × 12 months = 24 cells
        Assert.HasCount(24, result.PhotoHeatmap);
        Assert.HasCount(2, result.HeatmapYears);
        Assert.AreEqual(2024, result.HeatmapYears[0]); // Descending
        Assert.AreEqual(2023, result.HeatmapYears[1]);

        // Check specific cells
        var jan2024 = result.PhotoHeatmap.Single(c => c.Year == 2024 && c.Month == 1);
        Assert.AreEqual(2, jan2024.Count);
        Assert.IsTrue(jan2024.Level > 0);

        var jun2023 = result.PhotoHeatmap.Single(c => c.Year == 2023 && c.Month == 6);
        Assert.AreEqual(1, jun2023.Count);
        Assert.IsTrue(jun2023.Level > 0);

        // Empty months should have level 0
        var feb2024 = result.PhotoHeatmap.Single(c => c.Year == 2024 && c.Month == 2);
        Assert.AreEqual(0, feb2024.Count);
        Assert.AreEqual(0, feb2024.Level);
    }

    [TestMethod]
    public void Aggregate_PhotoHeatmap_EmptyWhenNoDateTaken()
    {
        // Arrange — images without DateTaken
        var images = new Dictionary<string, ImageContent>
        {
            ["nodate.jpg"] = TestData.Image("nodate.jpg")
        };
        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert
        Assert.IsEmpty(result.PhotoHeatmap);
        Assert.IsEmpty(result.HeatmapYears);
    }

    [TestMethod]
    public void Aggregate_PhotoHeatmap_LevelsAreQuartileBased()
    {
        // Arrange — create varied counts to test quartile distribution
        var images = new Dictionary<string, ImageContent>();
        var id = 0;

        // Jan 2024: 1 photo (low)
        images[$"img{id++}.jpg"] = TestData.Image($"img{id}.jpg", dateTaken: new DateTime(2024, 1, 1));

        // Feb 2024: 5 photos (medium)
        for (var i = 0; i < 5; i++)
        {
            images[$"img{id++}.jpg"] = TestData.Image($"img{id}.jpg", dateTaken: new DateTime(2024, 2, i + 1));
        }

        // Mar 2024: 10 photos (high)
        for (var i = 0; i < 10; i++)
        {
            images[$"img{id++}.jpg"] = TestData.Image($"img{id}.jpg", dateTaken: new DateTime(2024, 3, i + 1));
        }

        // Apr 2024: 50 photos (very high)
        for (var i = 0; i < 50; i++)
        {
            images[$"img{id++}.jpg"] = TestData.Image($"img{id}.jpg", dateTaken: new DateTime(2024, 4, (i % 28) + 1));
        }

        manifestRepository.Images.Returns(images);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Act
        var result = aggregator.Aggregate();

        // Assert — April (highest count) should have the highest level
        var apr = result.PhotoHeatmap.Single(c => c.Year == 2024 && c.Month == 4);
        Assert.IsTrue(apr.Level >= 3, $"Expected level >= 3 for highest count, got {apr.Level}");

        // Jan (lowest non-zero count) should have a lower level than April
        var jan = result.PhotoHeatmap.Single(c => c.Year == 2024 && c.Month == 1);
        Assert.IsTrue(jan.Level >= 1 && jan.Level <= apr.Level);

        var may = result.PhotoHeatmap.Single(c => c.Year == 2024 && c.Month == 5);
        Assert.AreEqual(0, may.Level);
    }
}
