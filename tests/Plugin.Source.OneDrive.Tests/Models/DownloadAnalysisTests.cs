using FluentAssertions;
using Spectara.Revela.Plugin.Source.OneDrive.Models;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Models;

[TestClass]
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
        result.Should().HaveCount(2);
        result.Should().Contain(i => i.RemoteItem.Name == "new.jpg");
        result.Should().Contain(i => i.RemoteItem.Name == "modified.jpg");
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
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Status == FileStatus.Unchanged);
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
        result.Should().BeEmpty();
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
        result.Should().HaveCount(3);
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
