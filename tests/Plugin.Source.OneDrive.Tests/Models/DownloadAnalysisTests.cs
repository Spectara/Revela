using Spectara.Revela.Plugin.Source.OneDrive.Models;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Models;

[TestClass]
[TestCategory("Unit")]
public sealed class DownloadAnalysisTests
{
    [TestMethod]
    public void ItemsToDownload_FiltersNewAndModified()
    {
        // Arrange
        var items = new List<DownloadItem>
        {
            CreateItem("new.jpg", FileStatus.New),
            CreateItem("modified.jpg", FileStatus.Modified),
            CreateItem("unchanged.jpg", FileStatus.Unchanged)
        };

        var analysis = new DownloadAnalysis
        {
            Items = items,
            OrphanedFiles = [],
            Statistics = new DownloadStatistics()
        };

        // Act
        var result = analysis.ItemsToDownload.ToList();

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.Any(i => i.RemoteItem.Name == "new.jpg"));
        Assert.IsTrue(result.Any(i => i.RemoteItem.Name == "modified.jpg"));
    }

    [TestMethod]
    public void UnchangedItems_FiltersOnlyUnchanged()
    {
        // Arrange
        var items = new List<DownloadItem>
        {
            CreateItem("new.jpg", FileStatus.New),
            CreateItem("modified.jpg", FileStatus.Modified),
            CreateItem("unchanged1.jpg", FileStatus.Unchanged),
            CreateItem("unchanged2.jpg", FileStatus.Unchanged)
        };

        var analysis = new DownloadAnalysis
        {
            Items = items,
            OrphanedFiles = [],
            Statistics = new DownloadStatistics()
        };

        // Act
        var result = analysis.UnchangedItems.ToList();

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.All(i => i.Status == FileStatus.Unchanged));
    }

    [TestMethod]
    public void ItemsToDownload_WhenEmpty_ReturnsEmpty()
    {
        // Arrange
        var items = new List<DownloadItem>
        {
            CreateItem("unchanged.jpg", FileStatus.Unchanged)
        };

        var analysis = new DownloadAnalysis
        {
            Items = items,
            OrphanedFiles = [],
            Statistics = new DownloadStatistics()
        };

        // Act
        var result = analysis.ItemsToDownload.ToList();

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void ItemsToDownload_WhenAllNew_ReturnsAll()
    {
        // Arrange
        var items = new List<DownloadItem>
        {
            CreateItem("new1.jpg", FileStatus.New),
            CreateItem("new2.jpg", FileStatus.New),
            CreateItem("new3.jpg", FileStatus.New)
        };

        var analysis = new DownloadAnalysis
        {
            Items = items,
            OrphanedFiles = [],
            Statistics = new DownloadStatistics()
        };

        // Act
        var result = analysis.ItemsToDownload.ToList();

        // Assert
        Assert.HasCount(3, result);
    }

    private static DownloadItem CreateItem(string name, FileStatus status)
    {
        return new DownloadItem
        {
            RemoteItem = new OneDriveItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Size = 1024,
                LastModified = DateTime.UtcNow
            },
            Status = status,
            Reason = status.ToString()
        };
    }
}
