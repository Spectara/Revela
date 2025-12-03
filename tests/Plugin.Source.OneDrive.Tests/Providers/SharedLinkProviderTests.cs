using System.Net;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Plugin.Source.OneDrive.Providers;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Providers;

/// <summary>
/// Unit tests for SharedLinkProvider using mocked HttpClient
/// </summary>
[TestClass]
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
        result.Should().BeEmpty();
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
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("file1");
        result[0].Name.Should().Be("photo.jpg");
        result[0].Size.Should().Be(1024);
        result[0].IsFolder.Should().BeFalse();
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
        result.Should().HaveCount(2);
        result.Should().Contain(i => i.Name == "Gallery" && i.IsFolder);
        result.Should().Contain(i => i.Name == "nested.jpg" && i.ParentPath == "Gallery");
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
        result[0].LastModified.Should().Be(expectedTime);
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
            result.Should().Be(tempPath);
            File.Exists(tempPath).Should().BeTrue();
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
            fileInfo.LastWriteTimeUtc.Should().Be(expectedTime);
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
            File.Exists(tempPath).Should().BeTrue();
            Directory.Exists(Path.Combine(tempBase, "nested", "folder")).Should().BeTrue();
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

        // Act
        Func<Task> act = async () => await provider.DownloadFileAsync(item, "C:\\temp\\photo.jpg");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*download URL*");
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

        // Act
        Func<Task> act = async () => await provider.DownloadFileAsync(item, "C:\\temp\\photo.jpg");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*download URL*");
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

        // Act
        Func<Task> act = async () => await provider.ListItemsAsync(config);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
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

        // Act
        Func<Task> act = async () => await provider.DownloadFileAsync(item, "C:\\temp\\photo.jpg");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
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

/// <summary>
/// Simple HTTP message handler for mocking HTTP responses in tests
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<Uri, HttpResponseMessage> responses = [];
    private readonly List<(Func<string, bool> Matcher, HttpResponseMessage Response)> patternResponses = [];

    public void AddResponse(Uri uri, HttpResponseMessage response) => responses[uri] = response;

    public void AddPatternResponse(Func<string, bool> urlMatcher, HttpResponseMessage response) => patternResponses.Add((urlMatcher, response));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;

        // Check exact match first
        if (responses.TryGetValue(uri, out var response))
        {
            return Task.FromResult(CloneResponse(response));
        }

        // Check pattern matches
        foreach (var (matcher, patternResponse) in patternResponses)
        {
            if (matcher(uri.ToString()))
            {
                return Task.FromResult(CloneResponse(patternResponse));
            }
        }

        // Default: not found
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage CloneResponse(HttpResponseMessage original)
    {
        // Clone to allow reuse
        var clone = new HttpResponseMessage(original.StatusCode);

        if (original.Content != null)
        {
            var contentBytes = original.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
