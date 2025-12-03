using FluentAssertions;
using Spectara.Revela.Plugin.Source.OneDrive.Models;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Models;

[TestClass]
public sealed class DownloadStatisticsTests
{
    [TestMethod]
    public void TotalFilesToDownload_ReturnsNewPlusModified()
    {
        // Arrange
        var stats = new DownloadStatistics
        {
            NewFiles = 5,
            ModifiedFiles = 3,
            UnchangedFiles = 10,
            OrphanedFiles = 2,
            TotalDownloadSize = 1000,
            TotalOrphanedSize = 500
        };

        // Act & Assert
        stats.TotalFilesToDownload.Should().Be(8); // 5 + 3
    }

    [TestMethod]
    public void TotalFiles_ReturnsAllNonOrphanedFiles()
    {
        // Arrange
        var stats = new DownloadStatistics
        {
            NewFiles = 5,
            ModifiedFiles = 3,
            UnchangedFiles = 10,
            OrphanedFiles = 2,
            TotalDownloadSize = 1000,
            TotalOrphanedSize = 500
        };

        // Act & Assert
        stats.TotalFiles.Should().Be(18); // 5 + 3 + 10
    }

    [TestMethod]
    public void TotalFilesToDownload_WhenNoChanges_ReturnsZero()
    {
        // Arrange
        var stats = new DownloadStatistics
        {
            NewFiles = 0,
            ModifiedFiles = 0,
            UnchangedFiles = 10,
            OrphanedFiles = 0,
            TotalDownloadSize = 0,
            TotalOrphanedSize = 0
        };

        // Act & Assert
        stats.TotalFilesToDownload.Should().Be(0);
    }

    [TestMethod]
    public void DefaultValues_AreZero()
    {
        // Arrange & Act
        var stats = new DownloadStatistics();

        // Assert
        stats.NewFiles.Should().Be(0);
        stats.ModifiedFiles.Should().Be(0);
        stats.UnchangedFiles.Should().Be(0);
        stats.OrphanedFiles.Should().Be(0);
        stats.TotalDownloadSize.Should().Be(0);
        stats.TotalOrphanedSize.Should().Be(0);
    }
}
