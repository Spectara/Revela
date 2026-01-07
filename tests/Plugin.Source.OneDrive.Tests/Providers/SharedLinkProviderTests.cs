using System.Net;
using System.Text.Json;
using NSubstitute;
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Plugin.Source.OneDrive.Providers;
using Spectara.Revela.Tests.Shared.Http;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Providers;

/// <summary>
/// Unit tests for SharedLinkProvider using mocked HttpClient
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class SharedLinkProviderTests : IDisposable
{
    private readonly MockHttpMessageHandler mockHandler;
    private readonly HttpClient httpClient;
    private readonly ILogger<SharedLinkProvider> logger;
    private readonly SharedLinkProvider provider;

    public SharedLinkProviderTests()
    {
        mockHandler = new MockHttpMessageHandler();
        httpClient = new HttpClient(mockHandler);
        logger = Substitute.For<ILogger<SharedLinkProvider>>();
        provider = new SharedLinkProvider(httpClient, logger);
    }

    public void Dispose()
    {
        httpClient.Dispose();
        mockHandler.Dispose();
    }

    #region ListItemsAsync Tests

    [TestMethod]
    public async Task ListItemsAsync_WithEmptyFolder_ReturnsEmptyList()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = "https://1drv.ms/f/s!example" };

        SetupBadgerTokenResponse();
        SetupActivationResponse("drive123", "folder123");
        SetupListItemsResponse([]);

        // Act
        var result = await provider.ListItemsAsync(config);

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task ListItemsAsync_WithSingleFile_ReturnsFile()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = "https://1drv.ms/f/s!example" };

        SetupBadgerTokenResponse();
        SetupActivationResponse("drive123", "folder123");
        SetupListItemsResponse(
        [
            new ItemData
            {
                Id = "file1",
                Name = "photo.jpg",
                Size = 1024,
                DownloadUrl = "https://cdn.example.com/photo.jpg",
                LastModified = "2024-01-15T10:30:00Z",
                MimeType = "image/jpeg"
            }
        ]);

        // Act
        var result = await provider.ListItemsAsync(config);

        // Assert
        Assert.HasCount(1, result);
        Assert.AreEqual("file1", result[0].Id);
        Assert.AreEqual("photo.jpg", result[0].Name);
        Assert.AreEqual(1024L, result[0].Size);
        Assert.IsFalse(result[0].IsFolder);
    }

    [TestMethod]
    public async Task ListItemsAsync_WithNestedFolders_RecursivelyListsItems()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = "https://1drv.ms/f/s!example" };

        SetupBadgerTokenResponse();
        SetupActivationResponse("drive123", "folder123");

        // Root folder with subfolder
        SetupListItemsResponse(
        [
            new ItemData { Id = "folder1", Name = "Gallery", IsFolder = true }
        ]);

        // Subfolder contents
        SetupSubfolderResponse("drive123", "folder123", "Gallery",
        [
            new ItemData
            {
                Id = "file2",
                Name = "nested.jpg",
                Size = 2048,
                DownloadUrl = "https://cdn.example.com/nested.jpg",
                LastModified = "2024-02-20T15:00:00Z"
            }
        ]);

        // Act
        var result = await provider.ListItemsAsync(config);

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.Any(i => i.Name == "Gallery" && i.IsFolder));
        Assert.IsTrue(result.Any(i => i.Name == "nested.jpg" && i.ParentPath == "Gallery"));
    }

    [TestMethod]
    public async Task ListItemsAsync_PreservesLastModifiedTimestamp()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = "https://1drv.ms/f/s!example" };
        var expectedTime = new DateTime(2024, 3, 15, 12, 45, 30, DateTimeKind.Utc);

        SetupBadgerTokenResponse();
        SetupActivationResponse("drive123", "folder123");
        SetupListItemsResponse(
        [
            new ItemData
            {
                Id = "file1",
                Name = "photo.jpg",
                Size = 1024,
                LastModified = "2024-03-15T12:45:30Z"
            }
        ]);

        // Act
        var result = await provider.ListItemsAsync(config);

        // Assert
        Assert.AreEqual(expectedTime, result[0].LastModified);
    }

    [TestMethod]
    public async Task ListItemsAsync_WithPagination_FetchesAllPages()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = "https://1drv.ms/f/s!example" };

        SetupBadgerTokenResponse();
        SetupActivationResponse("drive123", "folder123");

        // First page with nextLink
        SetupPaginatedListItemsResponse(
            [
                new ItemData { Id = "file1", Name = "photo1.jpg", Size = 1024 },
                new ItemData { Id = "file2", Name = "photo2.jpg", Size = 2048 }
            ],
            nextLink: "https://api.onedrive.com/v1.0/shares/u!xyz/root/children?$skiptoken=page2"
        );

        // Second page (no nextLink = last page)
        SetupNextPageResponse(
            "https://api.onedrive.com/v1.0/shares/u!xyz/root/children?$skiptoken=page2",
            [
                new ItemData { Id = "file3", Name = "photo3.jpg", Size = 3072 },
                new ItemData { Id = "file4", Name = "photo4.jpg", Size = 4096 }
            ],
            furtherNextLink: null
        );

        // Act
        var result = await provider.ListItemsAsync(config);

        // Assert - should have all 4 files from both pages
        Assert.HasCount(4, result);
        Assert.IsTrue(result.Any(i => i.Name == "photo1.jpg"));
        Assert.IsTrue(result.Any(i => i.Name == "photo2.jpg"));
        Assert.IsTrue(result.Any(i => i.Name == "photo3.jpg"));
        Assert.IsTrue(result.Any(i => i.Name == "photo4.jpg"));
    }

    [TestMethod]
    public async Task ListItemsAsync_WithPaginationInSubfolder_FetchesAllPagesRecursively()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = "https://1drv.ms/f/s!example" };

        SetupBadgerTokenResponse();
        SetupActivationResponse("drive123", "folder123");

        // Root folder with subfolder
        SetupListItemsResponse(
        [
            new ItemData { Id = "folder1", Name = "Gallery", IsFolder = true }
        ]);

        // Subfolder first page with nextLink
        SetupPaginatedSubfolderResponse(
            "drive123",
            "folder123",
            "Gallery",
            [
                new ItemData { Id = "file1", Name = "nested1.jpg", Size = 1024 },
                new ItemData { Id = "file2", Name = "nested2.jpg", Size = 2048 }
            ],
            nextLink: "https://api.onedrive.com/v1.0/drives/drive123/items/folder123:/Gallery:/children?$skiptoken=page2"
        );

        // Subfolder second page
        SetupNextPageResponse(
            "https://api.onedrive.com/v1.0/drives/drive123/items/folder123:/Gallery:/children?$skiptoken=page2",
            [
                new ItemData { Id = "file3", Name = "nested3.jpg", Size = 3072 }
            ],
            furtherNextLink: null
        );

        // Act
        var result = await provider.ListItemsAsync(config);

        // Assert - should have folder + 3 files from paginated subfolder
        Assert.HasCount(4, result);
        Assert.IsTrue(result.Any(i => i.Name == "Gallery" && i.IsFolder));
        Assert.IsTrue(result.Any(i => i.Name == "nested1.jpg" && i.ParentPath == "Gallery"));
        Assert.IsTrue(result.Any(i => i.Name == "nested2.jpg" && i.ParentPath == "Gallery"));
        Assert.IsTrue(result.Any(i => i.Name == "nested3.jpg" && i.ParentPath == "Gallery"));
    }

    #endregion

    #region DownloadFileAsync Tests

    [TestMethod]
    public async Task DownloadFileAsync_DownloadsToCorrectPath()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.jpg");
        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg");

        mockHandler.AddResponse(
            new Uri("https://cdn.example.com/photo.jpg"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("fake image data"u8.ToArray())
            }
        );

        try
        {
            // Act
            var result = await provider.DownloadFileAsync(item, tempPath);

            // Assert
            Assert.AreEqual(tempPath, result);
            Assert.IsTrue(File.Exists(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [TestMethod]
    public async Task DownloadFileAsync_SetsCorrectLastModifiedTime()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.jpg");
        var expectedTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg", expectedTime);

        mockHandler.AddResponse(
            new Uri("https://cdn.example.com/photo.jpg"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("fake image data"u8.ToArray())
            }
        );

        try
        {
            // Act
            await provider.DownloadFileAsync(item, tempPath);

            // Assert
            var fileInfo = new FileInfo(tempPath);
            Assert.AreEqual(expectedTime, fileInfo.LastWriteTimeUtc);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [TestMethod]
    public async Task DownloadFileAsync_CreatesNestedDirectories()
    {
        // Arrange
        var tempBase = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}");
        var tempPath = Path.Combine(tempBase, "nested", "folder", "photo.jpg");
        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg");

        mockHandler.AddResponse(
            new Uri("https://cdn.example.com/photo.jpg"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("fake image data"u8.ToArray())
            }
        );

        try
        {
            // Act
            await provider.DownloadFileAsync(item, tempPath);

            // Assert
            Assert.IsTrue(File.Exists(tempPath));
            Assert.IsTrue(Directory.Exists(Path.Combine(tempBase, "nested", "folder")));
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task DownloadFileAsync_WithoutDownloadUrl_ThrowsArgumentException()
    {
        // Arrange
        var item = new OneDriveItem
        {
            Id = "1",
            Name = "photo.jpg",
            DownloadUrl = null,
            Size = 1024,
            LastModified = DateTime.UtcNow
        };

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await provider.DownloadFileAsync(item, "C:\\temp\\photo.jpg"));
        Assert.IsTrue(ex.Message.Contains("download URL", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task DownloadFileAsync_WithEmptyDownloadUrl_ThrowsArgumentException()
    {
        // Arrange
        var item = new OneDriveItem
        {
            Id = "1",
            Name = "photo.jpg",
            DownloadUrl = string.Empty,
            Size = 1024,
            LastModified = DateTime.UtcNow
        };

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await provider.DownloadFileAsync(item, "C:\\temp\\photo.jpg"));
        Assert.IsTrue(ex.Message.Contains("download URL", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task ListItemsAsync_WhenBadgerTokenFails_ThrowsHttpRequestException()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = "https://1drv.ms/f/s!example" };

        mockHandler.AddResponse(
            new Uri("https://api-badgerp.svc.ms/v1.0/token"),
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
        );

        // Act & Assert
        await Assert.ThrowsExactlyAsync<HttpRequestException>(
            async () => await provider.ListItemsAsync(config));
    }

    [TestMethod]
    public async Task DownloadFileAsync_WhenDownloadFails_ThrowsHttpRequestException()
    {
        // Arrange
        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg");

        mockHandler.AddResponse(
            new Uri("https://cdn.example.com/photo.jpg"),
            new HttpResponseMessage(HttpStatusCode.NotFound)
        );

        // Act & Assert
        await Assert.ThrowsExactlyAsync<HttpRequestException>(
            async () => await provider.DownloadFileAsync(item, "C:\\temp\\photo.jpg"));
    }

    #endregion

    #region Helper Methods

    private void SetupBadgerTokenResponse(string token = "test-token-123")
    {
        mockHandler.AddResponse(
            new Uri("https://api-badgerp.svc.ms/v1.0/token"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { token }), System.Text.Encoding.UTF8, "application/json")
            }
        );
    }

    private void SetupActivationResponse(string driveId, string folderId)
    {
        // Match any activation URL (contains /shares/u!)
        mockHandler.AddPatternResponse(
            url => url.Contains("/shares/u!", StringComparison.Ordinal) && url.Contains("/driveItem", StringComparison.Ordinal),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        id = folderId,
                        parentReference = new { driveId }
                    }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            }
        );
    }

    private void SetupListItemsResponse(IEnumerable<ItemData> items)
    {
        var value = items.Select(i => CreateItemJson(i)).ToList();

        mockHandler.AddPatternResponse(
            url => url.Contains("/root/children", StringComparison.Ordinal),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { value }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            }
        );
    }

    private void SetupSubfolderResponse(string driveId, string folderId, string folderPath, IEnumerable<ItemData> items)
    {
        var value = items.Select(i => CreateItemJson(i)).ToList();

        // Match subfolder URL pattern: /drives/{driveId}/items/{folderId}:/{path}:/children
        mockHandler.AddPatternResponse(
            url => url.Contains($"/drives/{driveId}/items/{folderId}:", StringComparison.Ordinal) && url.Contains(folderPath, StringComparison.Ordinal),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { value }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            }
        );
    }

    private void SetupPaginatedListItemsResponse(IEnumerable<ItemData> items, string? nextLink)
    {
        var value = items.Select(i => CreateItemJson(i)).ToList();
        var response = new Dictionary<string, object> { ["value"] = value };

        if (nextLink is not null)
        {
            response["@odata.nextLink"] = nextLink;
        }

        mockHandler.AddPatternResponse(
            url => url.Contains("/root/children", StringComparison.Ordinal),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(response),
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            }
        );
    }

    private void SetupPaginatedSubfolderResponse(string driveId, string folderId, string folderPath, IEnumerable<ItemData> items, string? nextLink)
    {
        var value = items.Select(i => CreateItemJson(i)).ToList();
        var response = new Dictionary<string, object> { ["value"] = value };

        if (nextLink is not null)
        {
            response["@odata.nextLink"] = nextLink;
        }

        // Match subfolder URL pattern: /drives/{driveId}/items/{folderId}:/{path}:/children
        mockHandler.AddPatternResponse(
            url => url.Contains($"/drives/{driveId}/items/{folderId}:", StringComparison.Ordinal) && url.Contains(folderPath, StringComparison.Ordinal),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(response),
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            }
        );
    }

    private void SetupNextPageResponse(string nextLink, IEnumerable<ItemData> items, string? furtherNextLink)
    {
        var value = items.Select(i => CreateItemJson(i)).ToList();
        var response = new Dictionary<string, object> { ["value"] = value };

        if (furtherNextLink is not null)
        {
            response["@odata.nextLink"] = furtherNextLink;
        }

        mockHandler.AddResponse(
            new Uri(nextLink),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(response),
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            }
        );
    }

    private static Dictionary<string, object?> CreateItemJson(ItemData item)
    {
        var json = new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["name"] = item.Name,
            ["size"] = item.Size
        };

        if (item.IsFolder)
        {
            json["folder"] = new { };
        }
        else
        {
            json["file"] = new { mimeType = item.MimeType ?? "application/octet-stream" };
        }

        if (!string.IsNullOrEmpty(item.DownloadUrl))
        {
            json["@content.downloadUrl"] = item.DownloadUrl;
        }

        if (!string.IsNullOrEmpty(item.LastModified))
        {
            json["fileSystemInfo"] = new { lastModifiedDateTime = item.LastModified };
        }

        return json;
    }

    private static OneDriveItem CreateTestItem(string name, string downloadUrl, DateTime? lastModified = null)
    {
        return new OneDriveItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            DownloadUrl = downloadUrl,
            Size = 1024,
            LastModified = lastModified ?? DateTime.UtcNow
        };
    }

    #endregion

    #region Helper Classes

    private sealed class ItemData
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool IsFolder { get; init; }
        public long Size { get; init; }
        public string? DownloadUrl { get; init; }
        public string? LastModified { get; init; }
        public string? MimeType { get; init; }
    }

    #endregion
}
