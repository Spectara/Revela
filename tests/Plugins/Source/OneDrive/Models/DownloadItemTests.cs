using Spectara.Revela.Plugins.Source.OneDrive.Models;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Models;

[TestClass]
[TestCategory("Unit")]
public sealed class DownloadItemTests
{
    [TestMethod]
    public void RelativePath_WhenNoParent_ReturnsFileName()
    {
        // Arrange
        var item = new DownloadItem
        {
            RemoteItem = new OneDriveItem
            {
                Id = "1",
                Name = "photo.jpg",
                ParentPath = string.Empty,
                Size = 1024,
                LastModified = DateTime.UtcNow
            },
            Status = FileStatus.New,
            Reason = "New file"
        };

        // Act & Assert
        Assert.AreEqual("photo.jpg", item.RelativePath);
    }

    [TestMethod]
    public void RelativePath_WithParent_ReturnsFullPath()
    {
        // Arrange
        var item = new DownloadItem
        {
            RemoteItem = new OneDriveItem
            {
                Id = "1",
                Name = "photo.jpg",
                ParentPath = "Gallery/2024",
                Size = 1024,
                LastModified = DateTime.UtcNow
            },
            Status = FileStatus.New,
            Reason = "New file"
        };

        // Act & Assert
        Assert.AreEqual("Gallery/2024/photo.jpg", item.RelativePath);
    }

    [TestMethod]
    public void RelativePath_NormalizesBackslashesToForward()
    {
        // Arrange
        var item = new DownloadItem
        {
            RemoteItem = new OneDriveItem
            {
                Id = "1",
                Name = "photo.jpg",
                ParentPath = "Gallery\\Subfolder",
                Size = 1024,
                LastModified = DateTime.UtcNow
            },
            Status = FileStatus.New,
            Reason = "New file"
        };

        // Act
        var result = item.RelativePath;

        // Assert
        Assert.DoesNotContain("\\", result);
        Assert.Contains("/", result);
    }
}
