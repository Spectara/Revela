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
        Assert.AreEqual("abc123", item.Id);
        Assert.AreEqual("photo.jpg", item.Name);
        Assert.IsFalse(item.IsFolder);
        Assert.AreEqual(1024L, item.Size);
        Assert.AreEqual("https://cdn.onedrive.com/file/photo.jpg", item.DownloadUrl);
        Assert.AreEqual(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc), item.LastModified);
        Assert.AreEqual("image/jpeg", item.MimeType);
        Assert.AreEqual("Gallery/2024", item.ParentPath);
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
        Assert.IsTrue(item.IsFolder);
        Assert.AreEqual(0L, item.Size);
        Assert.IsNull(item.DownloadUrl);
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
        Assert.AreEqual(string.Empty, item.ParentPath);
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
        Assert.IsNull(item.MimeType);
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
        Assert.AreEqual(DateTimeKind.Utc, item.LastModified.Kind);
    }
}
