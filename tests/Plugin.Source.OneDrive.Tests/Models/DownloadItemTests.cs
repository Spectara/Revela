using FluentAssertions;
using Spectara.Revela.Plugin.Source.OneDrive.Models;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Models;

[TestClass]
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
        item.RelativePath.Should().Be("photo.jpg");
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
        item.RelativePath.Should().Be("Gallery/2024/photo.jpg");
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
        result.Should().NotContain("\\");
        result.Should().Contain("/");
    }

    [TestMethod]
    public void LocalFile_WhenNull_IsAllowed()
    {
        // Arrange & Act
        var item = new DownloadItem
        {
            RemoteItem = new OneDriveItem
            {
                Id = "1",
                Name = "new-file.jpg",
                Size = 1024,
                LastModified = DateTime.UtcNow
            },
            LocalFile = null, // File doesn't exist locally
            Status = FileStatus.New,
            Reason = "New file"
        };

        // Assert
        item.LocalFile.Should().BeNull();
        item.Status.Should().Be(FileStatus.New);
    }
}
