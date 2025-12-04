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
        Assert.AreEqual(8, stats.TotalFilesToDownload); // 5 + 3
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
        Assert.AreEqual(18, stats.TotalFiles); // 5 + 3 + 10
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
        Assert.AreEqual(0, stats.TotalFilesToDownload);
    }

    [TestMethod]
    public void DefaultValues_AreZero()
    {
        // Arrange & Act
        var stats = new DownloadStatistics();

        // Assert
        Assert.AreEqual(0, stats.NewFiles);
        Assert.AreEqual(0, stats.ModifiedFiles);
        Assert.AreEqual(0, stats.UnchangedFiles);
        Assert.AreEqual(0, stats.OrphanedFiles);
        Assert.AreEqual(0L, stats.TotalDownloadSize);
        Assert.AreEqual(0L, stats.TotalOrphanedSize);
    }
}
