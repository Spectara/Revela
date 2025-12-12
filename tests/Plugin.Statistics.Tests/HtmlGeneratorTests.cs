using Spectara.Revela.Plugin.Statistics.Models;
using Spectara.Revela.Plugin.Statistics.Services;

namespace Spectara.Revela.Plugin.Statistics.Tests;

[TestClass]
public sealed class HtmlGeneratorTests
{
    [TestMethod]
    public void Generate_IncludesMarkers()
    {
        // Arrange
        var stats = CreateTestStats();

        // Act
        var result = HtmlGenerator.Generate(stats);

        // Assert
        Assert.IsTrue(result.StartsWith(HtmlGenerator.BeginMarker, StringComparison.Ordinal));
        Assert.IsTrue(result.TrimEnd().EndsWith(HtmlGenerator.EndMarker, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_IncludesOverviewSection()
    {
        // Arrange
        var stats = CreateTestStats();

        // Act
        var result = HtmlGenerator.Generate(stats);

        // Assert
        Assert.IsTrue(result.Contains("stats-overview", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("100", StringComparison.Ordinal)); // TotalImages
        Assert.IsTrue(result.Contains("95", StringComparison.Ordinal));  // ImagesWithExif
        Assert.IsTrue(result.Contains("10", StringComparison.Ordinal));  // TotalGalleries
    }

    [TestMethod]
    public void Generate_IncludesBarChartWithPercentage()
    {
        // Arrange
        var stats = new SiteStatistics
        {
            TotalImages = 100,
            ImagesWithExif = 100,
            Apertures =
            [
                new StatisticsEntry { Label = "f/1.4-2.0", Count = 50, Percent = 100 },
                new StatisticsEntry { Label = "f/2.8-4.0", Count = 25, Percent = 50 }
            ]
        };

        // Act
        var result = HtmlGenerator.Generate(stats);

        // Assert
        Assert.IsTrue(result.Contains("stats-bar", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("stats-fill", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("--percent: 100%", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("--percent: 50%", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("f/1.4-2.0", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_EscapesHtmlInLabels()
    {
        // Arrange
        var stats = new SiteStatistics
        {
            TotalImages = 1,
            CameraModels =
            [
                new StatisticsEntry { Label = "Test <Camera> & \"Model\"", Count = 1, Percent = 100 }
            ]
        };

        // Act
        var result = HtmlGenerator.Generate(stats);

        // Assert
        Assert.IsTrue(result.Contains("Test &lt;Camera&gt; &amp; &quot;Model&quot;", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("Test <Camera>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_SkipsEmptyCategories()
    {
        // Arrange
        var stats = new SiteStatistics
        {
            TotalImages = 100,
            ImagesWithExif = 100,
            CameraModels = [], // Empty
            Apertures =
            [
                new StatisticsEntry { Label = "f/2.8", Count = 50, Percent = 100 }
            ]
        };

        // Act
        var result = HtmlGenerator.Generate(stats);

        // Assert
        Assert.IsTrue(result.Contains("Aperture", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("Camera Models", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_IncludesTimestamp()
    {
        // Arrange
        var stats = new SiteStatistics
        {
            TotalImages = 1,
            GeneratedAt = new DateTime(2024, 12, 15, 14, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var result = HtmlGenerator.Generate(stats);

        // Assert
        Assert.IsTrue(result.Contains("2024-12-15 14:30 UTC", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("stats-generated", StringComparison.Ordinal));
    }

    private static SiteStatistics CreateTestStats() => new()
    {
        TotalImages = 100,
        ImagesWithExif = 95,
        TotalGalleries = 10,
        CameraModels =
        [
            new StatisticsEntry { Label = "Sony A7IV", Count = 60, Percent = 100 },
            new StatisticsEntry { Label = "Canon R5", Count = 40, Percent = 67 }
        ],
        Apertures =
        [
            new StatisticsEntry { Label = "f/1.4-2.0", Count = 45, Percent = 100 },
            new StatisticsEntry { Label = "f/2.8-4.0", Count = 30, Percent = 67 }
        ],
        GeneratedAt = DateTime.UtcNow
    };
}
