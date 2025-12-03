using FluentAssertions;
using Spectara.Revela.Plugin.Source.OneDrive.Models;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Models;

[TestClass]
public sealed class OneDriveItemTests
{
    [TestMethod]
    public void OneDriveItem_File_HasCorrectProperties()
    {
        // Arrange & Act
        var item = new OneDriveItem
        {
            Id = "abc123",
            Name = "photo.jpg",
            IsFolder = false,
            Size = 1024,
            DownloadUrl = "https://cdn.onedrive.com/file/photo.jpg",
            LastModified = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            MimeType = "image/jpeg",
            ParentPath = "Gallery/2024"
        };

        // Assert
        item.Id.Should().Be("abc123");
        item.Name.Should().Be("photo.jpg");
        item.IsFolder.Should().BeFalse();
        item.Size.Should().Be(1024);
        item.DownloadUrl.Should().Be("https://cdn.onedrive.com/file/photo.jpg");
        item.LastModified.Should().Be(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        item.MimeType.Should().Be("image/jpeg");
        item.ParentPath.Should().Be("Gallery/2024");
    }

    [TestMethod]
    public void OneDriveItem_Folder_HasZeroSizeAndNoDownloadUrl()
    {
        // Arrange & Act
        var item = new OneDriveItem
        {
            Id = "folder123",
            Name = "Gallery",
            IsFolder = true,
            Size = 0,
            DownloadUrl = null,
            LastModified = DateTime.UtcNow
        };

        // Assert
        item.IsFolder.Should().BeTrue();
        item.Size.Should().Be(0);
        item.DownloadUrl.Should().BeNull();
    }

    [TestMethod]
    public void OneDriveItem_ParentPath_DefaultsToEmpty()
    {
        // Arrange & Act
        var item = new OneDriveItem
        {
            Id = "1",
            Name = "root-file.jpg",
            Size = 100,
            LastModified = DateTime.UtcNow
        };

        // Assert
        item.ParentPath.Should().BeEmpty();
    }

    [TestMethod]
    public void OneDriveItem_MimeType_CanBeNull()
    {
        // Arrange & Act
        var item = new OneDriveItem
        {
            Id = "1",
            Name = "file.unknown",
            Size = 100,
            LastModified = DateTime.UtcNow,
            MimeType = null
        };

        // Assert
        item.MimeType.Should().BeNull();
    }

    [TestMethod]
    public void OneDriveItem_LastModified_PreservesUtcKind()
    {
        // Arrange
        var utcTime = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var item = new OneDriveItem
        {
            Id = "1",
            Name = "test.jpg",
            Size = 100,
            LastModified = utcTime
        };

        // Assert
        item.LastModified.Kind.Should().Be(DateTimeKind.Utc);
    }
}
