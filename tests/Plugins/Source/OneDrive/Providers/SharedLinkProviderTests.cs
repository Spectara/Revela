using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectara.Revela.Plugins.Source.OneDrive;
using Spectara.Revela.Plugins.Source.OneDrive.Models;
using Spectara.Revela.Plugins.Source.OneDrive.Providers;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Tests.Shared.Http;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Providers;

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
        SetupBadgerTokenResponse();
        SetupActivationResponse("drive123", "folder123");
        SetupListItemsResponse([]);

        // Act
        var result = await provider.ListItemsAsync("https://1drv.ms/f/s!example");

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task ListItemsAsync_WithSingleFile_ReturnsFile()
    {
        // Arrange
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
        var result = await provider.ListItemsAsync("https://1drv.ms/f/s!example");

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
        var result = await provider.ListItemsAsync("https://1drv.ms/f/s!example");

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.Any(i => i.Name == "Gallery" && i.IsFolder));
        Assert.IsTrue(result.Any(i => i.Name == "nested.jpg" && i.ParentPath == "Gallery"));
    }

    [TestMethod]
    public async Task ListItemsAsync_PreservesLastModifiedTimestamp()
    {
        // Arrange
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
        var result = await provider.ListItemsAsync("https://1drv.ms/f/s!example");

        // Assert
        Assert.AreEqual(expectedTime, result[0].LastModified);
    }

    [TestMethod]
    public async Task ListItemsAsync_WithPagination_FetchesAllPages()
    {
        // Arrange
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
        var result = await provider.ListItemsAsync("https://1drv.ms/f/s!example");

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
        var result = await provider.ListItemsAsync("https://1drv.ms/f/s!example");

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
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    [DataRow(true, false)]
    public async Task DownloadFileAsync_WhenBodyInterrupted_PreservesDestinationState(bool cancel, bool destinationExists)
    {
        using var project = TestProject.Create();
        using var cancellationSource = new CancellationTokenSource();
        var destinationPath = Path.Combine(project.SourcePath, "photo.jpg");
        var previousBytes = "previous image bytes"u8.ToArray();
        var previousTimestamp = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        if (destinationExists)
        {
            await File.WriteAllBytesAsync(destinationPath, previousBytes);
            File.SetLastWriteTimeUtc(destinationPath, previousTimestamp);
        }

        var unrelatedPath = destinationPath + ".unrelated.tmp";
        await File.WriteAllBytesAsync(unrelatedPath, previousBytes);
        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg");
        using var body = new DownloadBodyStream("partial replacement"u8.ToArray(), () =>
        {
            if (cancel)
            {
                cancellationSource.Cancel();
            }
            else
            {
                throw new IOException("Synthetic body interruption");
            }
        });
        using var handler = new StreamingHttpMessageHandler(body);
        using var client = new HttpClient(handler);
        var streamingProvider = new SharedLinkProvider(client, logger);

        if (cancel)
        {
            var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => streamingProvider.DownloadFileAsync(item, destinationPath, cancellationSource.Token));
            Assert.AreEqual(cancellationSource.Token, exception.CancellationToken);
        }
        else
        {
            var exception = await Assert.ThrowsExactlyAsync<IOException>(
                () => streamingProvider.DownloadFileAsync(item, destinationPath));
            Assert.AreEqual("Synthetic body interruption", exception.Message);
        }

        Assert.AreEqual(2, body.ReadCalls);
        Assert.AreEqual("partial replacement"u8.Length, body.BytesRead);
        if (destinationExists)
        {
            CollectionAssert.AreEqual(previousBytes, await File.ReadAllBytesAsync(destinationPath));
            Assert.AreEqual(previousTimestamp, File.GetLastWriteTimeUtc(destinationPath));
        }
        else
        {
            Assert.IsFalse(File.Exists(destinationPath));
        }

        CollectionAssert.AreEqual(previousBytes, await File.ReadAllBytesAsync(unrelatedPath));
        CollectionAssert.AreEquivalent(
            destinationExists ? new[] { destinationPath, unrelatedPath } : [unrelatedPath],
            Directory.GetFiles(project.SourcePath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DownloadFileAsync_WhenSuccessful_PublishesBytesAndTimestamp(bool destinationExists)
    {
        using var project = TestProject.Create();
        var destinationPath = Path.Combine(project.SourcePath, "photo.jpg");
        if (destinationExists)
        {
            await File.WriteAllBytesAsync(destinationPath, "previous image bytes"u8.ToArray());
            File.SetLastWriteTimeUtc(destinationPath, new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        }

        var expectedBytes = "complete replacement image bytes"u8.ToArray();
        var expectedTimestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg", expectedTimestamp);
        using var body = new DownloadBodyStream(expectedBytes);
        using var handler = new StreamingHttpMessageHandler(body);
        using var client = new HttpClient(handler);
        var streamingProvider = new SharedLinkProvider(client, logger);

        var result = await streamingProvider.DownloadFileAsync(item, destinationPath);

        Assert.AreEqual(destinationPath, result);
        Assert.AreEqual(2, body.ReadCalls);
        Assert.AreEqual(expectedBytes.Length, body.BytesRead);
        CollectionAssert.AreEqual(expectedBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.AreEqual(expectedTimestamp, File.GetLastWriteTimeUtc(destinationPath));
        CollectionAssert.AreEquivalent(new[] { destinationPath }, Directory.GetFiles(project.SourcePath));
    }

    [TestMethod]
    [DataRow(false, UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [DataRow(true, UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [DataRow(true, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead)]
    public async Task DownloadFileAsync_UnixPermissions_CreatesPrivateStageAndPreservesDestinationMode(
        bool destinationExists, UnixFileMode finalMode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file permissions cannot be verified on Windows.");
            return;
        }

        using var project = TestProject.Create();
        var destinationPath = Path.Combine(project.SourcePath, "photo.jpg");
        var previousTimestamp = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        if (destinationExists)
        {
            await File.WriteAllBytesAsync(destinationPath, "previous image bytes"u8.ToArray());
            File.SetLastWriteTimeUtc(destinationPath, previousTimestamp);
            File.SetUnixFileMode(destinationPath, finalMode);
        }

        var expectedBytes = Encoding.UTF8.GetBytes(new string('x', 16384));
        var expectedTimestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg", expectedTimestamp);
        var stagingObservations = 0;
        using var body = new DownloadBodyStream(expectedBytes, onRead: () =>
        {
            var temporaryFiles = Directory.GetFiles(project.SourcePath, "photo.jpg.*.tmp");
            Assert.HasCount(1, temporaryFiles);
            if (!OperatingSystem.IsWindows())
            {
                Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(temporaryFiles[0]));
            }

            Assert.AreEqual(stagingObservations == 0 ? 0L : expectedBytes.Length, new FileInfo(temporaryFiles[0]).Length);
            if (destinationExists)
            {
                Assert.AreEqual(previousTimestamp, File.GetLastWriteTimeUtc(destinationPath));
            }
            else
            {
                Assert.IsFalse(File.Exists(destinationPath));
            }

            stagingObservations++;
        });
        using var handler = new StreamingHttpMessageHandler(body);
        using var client = new HttpClient(handler);
        var streamingProvider = new SharedLinkProvider(client, logger);

        var result = await streamingProvider.DownloadFileAsync(item, destinationPath);

        Assert.AreEqual(destinationPath, result);
        Assert.AreEqual(2, stagingObservations);
        Assert.AreEqual(finalMode, File.GetUnixFileMode(destinationPath));
        CollectionAssert.AreEqual(expectedBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.AreEqual(expectedTimestamp, File.GetLastWriteTimeUtc(destinationPath));
        CollectionAssert.AreEquivalent(new[] { destinationPath }, Directory.GetFiles(project.SourcePath));
    }

    [TestMethod]
    public async Task DownloadFileAsync_WhenPublicationFails_PreservesDirectoryContentsAndRemovesTemporaryFile()
    {
        using var project = TestProject.Create();
        var destinationPath = Path.Combine(project.SourcePath, "photo.jpg");
        Directory.CreateDirectory(destinationPath);
        var retainedPath = Path.Combine(destinationPath, "retained.jpg");
        var previousBytes = "previous image bytes"u8.ToArray();
        var previousTimestamp = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        await File.WriteAllBytesAsync(retainedPath, previousBytes);
        File.SetLastWriteTimeUtc(retainedPath, previousTimestamp);
        var replacementBytes = "complete replacement"u8.ToArray();
        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg");
        using var body = new DownloadBodyStream(replacementBytes);
        using var handler = new StreamingHttpMessageHandler(body);
        using var client = new HttpClient(handler);
        var streamingProvider = new SharedLinkProvider(client, logger);

        if (OperatingSystem.IsWindows())
        {
            await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
                () => streamingProvider.DownloadFileAsync(item, destinationPath));
        }
        else
        {
            await Assert.ThrowsExactlyAsync<IOException>(
                () => streamingProvider.DownloadFileAsync(item, destinationPath));
        }

        Assert.AreEqual(2, body.ReadCalls);
        Assert.AreEqual(replacementBytes.Length, body.BytesRead);
        CollectionAssert.AreEqual(previousBytes, await File.ReadAllBytesAsync(retainedPath));
        Assert.AreEqual(previousTimestamp, File.GetLastWriteTimeUtc(retainedPath));
        Assert.IsEmpty(Directory.GetFiles(project.SourcePath));
        CollectionAssert.AreEquivalent(new[] { destinationPath }, Directory.GetDirectories(project.SourcePath));
        CollectionAssert.AreEquivalent(new[] { retainedPath }, Directory.GetFiles(destinationPath));
    }

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
        using var project = TestProject.Create();
        var destinationPath = Path.Combine(project.SourcePath, "photo.jpg");
        var previousBytes = "previous image bytes"u8.ToArray();
        var previousTimestamp = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        await File.WriteAllBytesAsync(destinationPath, previousBytes);
        File.SetLastWriteTimeUtc(destinationPath, previousTimestamp);
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
            async () => await provider.DownloadFileAsync(item, destinationPath));
        Assert.IsTrue(ex.Message.Contains("download URL", StringComparison.OrdinalIgnoreCase));
        CollectionAssert.AreEqual(previousBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.AreEqual(previousTimestamp, File.GetLastWriteTimeUtc(destinationPath));
        CollectionAssert.AreEquivalent(new[] { destinationPath }, Directory.GetFiles(project.SourcePath));
    }

    [TestMethod]
    public async Task DownloadFileAsync_WithEmptyDownloadUrl_ThrowsArgumentException()
    {
        // Arrange
        using var project = TestProject.Create();
        var destinationPath = Path.Combine(project.SourcePath, "photo.jpg");
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
            async () => await provider.DownloadFileAsync(item, destinationPath));
        Assert.IsTrue(ex.Message.Contains("download URL", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(File.Exists(destinationPath));
        Assert.IsEmpty(Directory.GetFiles(project.SourcePath));
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    [DataRow("success", 4)]
    [DataRow("share-failure", 5)]
    [DataRow("cdn-failure", 7)]
    [DataRow("cdn-status", 7)]
    public async Task ConfigureServices_TraceHttpLogs_DoNotDiscloseCredentials(string scenario, int expectedRequests)
    {
        using var project = TestProject.CreateMinimal();
        using var capture = new PrivacyLogCapture();
        using var handler = new PrivacyHttpMessageHandler(scenario);
        var services = CreatePrivacyServices(handler, capture);
        using var serviceProvider = services.BuildServiceProvider();
        PrivacyLogCapture.WriteControl(serviceProvider.GetRequiredService<ILoggerFactory>());
        var typedProvider = serviceProvider.GetRequiredService<SharedLinkProvider>();
        var destination = Path.Combine(project.SourcePath, "photo.jpg");

        async Task SyncAsync()
        {
            var items = await typedProvider.ListItemsAsync(PrivacyHttpMessageHandler.ShareUrl);
            Assert.HasCount(1, items);
            await typedProvider.DownloadFileAsync(items[0], destination);
        }

        if (string.Equals(scenario, "success", StringComparison.Ordinal))
        {
            await SyncAsync();
            Assert.AreEqual("synthetic image bytes", await File.ReadAllTextAsync(destination));
        }
        else
        {
            await Assert.ThrowsExactlyAsync<HttpRequestException>(SyncAsync);
            Assert.IsFalse(File.Exists(destination));
        }

        Assert.AreEqual(expectedRequests, handler.Requests);
        Assert.IsTrue(handler.BadgerRequests > 0, "The local handler must observe authenticated share requests.");
        Assert.IsTrue(handler.BearerRequests > 0, "The local handler must observe the injected synthetic bearer header.");
        Assert.IsTrue(capture.Entries.Any(entry => entry.Contains("Listed", StringComparison.Ordinal) || entry.Contains("Requesting Badger", StringComparison.Ordinal)));
        Assert.IsTrue(capture.Entries.Any(entry => entry.Contains("OneDrive HTTP event", StringComparison.Ordinal)), "Safe resilience outcome logging must remain active.");
        if (scenario.EndsWith("failure", StringComparison.Ordinal))
        {
            Assert.IsTrue(capture.Entries.Contains("state:HttpRequestException"), "Resilience failures must retain their sanitized category.");
        }
        else
        {
            Assert.IsTrue(capture.Entries.Contains(string.Equals(scenario, "cdn-status", StringComparison.Ordinal) ? "state:503" : "state:200"));
        }
        capture.AssertNoCredentials();
    }

    [TestMethod]
    public async Task ListItemsAsync_WhenBadgerTokenFails_ThrowsHttpRequestException()
    {
        // Arrange
        mockHandler.AddResponse(
            new Uri("https://api-badgerp.svc.ms/v1.0/token"),
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
        );

        // Act & Assert
        await Assert.ThrowsExactlyAsync<HttpRequestException>(
            async () => await provider.ListItemsAsync("https://1drv.ms/f/s!example"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DownloadFileAsync_WhenDownloadFails_ThrowsHttpRequestException(bool destinationExists)
    {
        // Arrange
        using var project = TestProject.Create();
        var destinationPath = Path.Combine(project.SourcePath, "photo.jpg");
        var previousBytes = "previous image bytes"u8.ToArray();
        var previousTimestamp = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        if (destinationExists)
        {
            await File.WriteAllBytesAsync(destinationPath, previousBytes);
            File.SetLastWriteTimeUtc(destinationPath, previousTimestamp);
        }

        var item = CreateTestItem("photo.jpg", "https://cdn.example.com/photo.jpg");

        mockHandler.AddResponse(
            new Uri("https://cdn.example.com/photo.jpg"),
            new HttpResponseMessage(HttpStatusCode.NotFound)
        );

        // Act & Assert
        await Assert.ThrowsExactlyAsync<HttpRequestException>(
            async () => await provider.DownloadFileAsync(item, destinationPath));

        if (destinationExists)
        {
            CollectionAssert.AreEqual(previousBytes, await File.ReadAllBytesAsync(destinationPath));
            Assert.AreEqual(previousTimestamp, File.GetLastWriteTimeUtc(destinationPath));
        }
        else
        {
            Assert.IsFalse(File.Exists(destinationPath));
        }

        CollectionAssert.AreEquivalent(
            destinationExists ? new[] { destinationPath } : [],
            Directory.GetFiles(project.SourcePath));
    }

    #endregion

    #region Helper Methods

    private void SetupBadgerTokenResponse(string token = "test-token-123")
    {
        mockHandler.AddResponse(
            new Uri("https://api-badgerp.svc.ms/v1.0/token"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { token }), Encoding.UTF8, "application/json")
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
                    Encoding.UTF8,
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
                    Encoding.UTF8,
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
                    Encoding.UTF8,
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
                    Encoding.UTF8,
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
                    Encoding.UTF8,
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
                    Encoding.UTF8,
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

    internal static ServiceCollection CreatePrivacyServices(PrivacyHttpMessageHandler handler, PrivacyLogCapture capture)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        new OneDrivePlugin().ConfigureServices(services);
        services.AddHttpClient<SharedLinkProvider>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .ConfigureHttpClient(client => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrivacyHttpMessageHandler.BearerToken))
            .RedactLoggedHeaders(static _ => false);
        return services;
    }

    internal sealed class PrivacyHttpMessageHandler(string scenario = "success", Func<Exception>? failureFactory = null) : HttpMessageHandler
    {
        internal const string ShareUrl = "https://1drv.ms/f/SYNTHETIC_SHARE_SECRET?authkey=SYNTHETIC_SHARE_QUERY";
        internal const string CdnUrl = "https://cdn.example.com/SYNTHETIC_CDN_PATH/photo.jpg?sig=SYNTHETIC_CDN_QUERY";
        internal const string BadgerToken = "SYNTHETIC_BADGER_TOKEN";
        internal const string BearerToken = "SYNTHETIC_BEARER_TOKEN";
        internal const string FailureMessage = ShareUrl + " " + CdnUrl + " " + BadgerToken + " " + BearerToken + " SYNTHETIC_EXCEPTION_SECRET";

        public int Requests { get; private set; }
        public int BadgerRequests { get; private set; }
        public int BearerRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (string.Equals(request.Headers.Authorization?.Parameter, BadgerToken, StringComparison.Ordinal))
            {
                BadgerRequests++;
            }
            if (string.Equals(request.Headers.Authorization?.Parameter, BearerToken, StringComparison.Ordinal))
            {
                BearerRequests++;
            }

            var uri = request.RequestUri!;
            if (string.Equals(uri.Host, "api-badgerp.svc.ms", StringComparison.Ordinal))
            {
                return JsonResponse(new { token = BadgerToken });
            }
            if (string.Equals(uri.Host, "cdn.example.com", StringComparison.Ordinal))
            {
                Assert.IsTrue(string.Equals(uri.AbsoluteUri, CdnUrl, StringComparison.Ordinal), "The signed CDN URL must reach the local primary handler unchanged.");
                if (string.Equals(scenario, "cdn-status", StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        ReasonPhrase = FailureMessage,
                        Content = new StringContent(FailureMessage)
                    });
                }
                if (!string.Equals(scenario, "success", StringComparison.Ordinal))
                {
                    return Task.FromException<HttpResponseMessage>(failureFactory?.Invoke() ?? new HttpRequestException(FailureMessage));
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("synthetic image bytes") });
            }
            if (uri.AbsolutePath.EndsWith("/driveItem", StringComparison.Ordinal))
            {
                if (string.Equals(scenario, "share-failure", StringComparison.Ordinal))
                {
                    return Task.FromException<HttpResponseMessage>(new HttpRequestException(FailureMessage));
                }
                return JsonResponse(new { id = "folder123", parentReference = new { driveId = "drive123" } });
            }
            Assert.IsTrue(uri.AbsolutePath.EndsWith("/root/children", StringComparison.Ordinal), "Unexpected request reached the local handler.");
            return JsonResponse(new
            {
                value = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["id"] = "photo123",
                        ["name"] = "photo.jpg",
                        ["size"] = 21,
                        ["file"] = new { mimeType = "image/jpeg" },
                        ["@content.downloadUrl"] = CdnUrl
                    }
                }
            });
        }

        private static Task<HttpResponseMessage> JsonResponse<T>(T value) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
            });
    }

    internal sealed class PrivacyLogCapture : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider externalScopes = new LoggerExternalScopeProvider();
        private readonly ConcurrentQueue<Exception> exceptions = new();
        internal ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => externalScopes = scopeProvider;
        public void Dispose() { }

        internal static void WriteControl(ILoggerFactory factory)
        {
            var control = factory.CreateLogger("PrivacyControl");
            if (control.IsEnabled(LogLevel.Trace))
            {
                using var scope = control.BeginScope(new Dictionary<string, object> { ["ControlScope"] = "privacy-control-scope" });
                control.Log(LogLevel.Trace, new EventId(901), new Dictionary<string, object> { ["ControlState"] = "privacy-control-state" }, new InvalidOperationException("privacy-control-exception"),
                    static (_, _) => "privacy-control-formatted");
            }
        }

        internal void AssertNoCredentials()
        {
            Assert.IsTrue(Entries.Contains("formatted:privacy-control-formatted"), "Trace logging capture was not active.");
            Assert.IsTrue(Entries.Contains("state:privacy-control-state"), "Structured state capture was not active.");
            Assert.IsTrue(Entries.Contains("scope:privacy-control-scope"), "Scope capture was not active.");
            var encodedShare = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrivacyHttpMessageHandler.ShareUrl)).TrimEnd('=').Replace('/', '_').Replace('+', '-');
            var sentinels = new[]
            {
                "SYNTHETIC_SHARE_SECRET", "SYNTHETIC_SHARE_QUERY", "SYNTHETIC_CDN_PATH", "SYNTHETIC_CDN_QUERY",
                PrivacyHttpMessageHandler.BadgerToken, PrivacyHttpMessageHandler.BearerToken, "SYNTHETIC_EXCEPTION_SECRET",
                encodedShare, Uri.EscapeDataString(PrivacyHttpMessageHandler.ShareUrl), Uri.EscapeDataString(PrivacyHttpMessageHandler.CdnUrl)
            };
            foreach (var sentinel in sentinels)
            {
                Assert.IsFalse(Entries.Any(entry => entry.Contains(sentinel, StringComparison.Ordinal)), "A synthetic credential reached a captured logging surface.");
            }
            Assert.HasCount(1, exceptions, "Only the benign positive-control exception may reach the log sink.");
            Assert.IsTrue(Entries.Any(entry => entry.StartsWith("exception:System.InvalidOperationException: privacy-control-exception", StringComparison.Ordinal)), "Exception capture was not active.");
        }

        private void Capture(string surface, object? value)
        {
            Entries.Enqueue(surface + ":" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            if (value is Exception exception)
            {
                foreach (System.Collections.DictionaryEntry property in exception.Data)
                {
                    Capture(surface, property.Key);
                    Capture(surface, property.Value);
                }
            }
            if (value is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                foreach (var property in properties)
                {
                    Capture(surface, property.Key);
                    Capture(surface, property.Value);
                }
            }
        }

        private sealed class CaptureLogger(PrivacyLogCapture capture, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                capture.Capture("scope", state);
                return capture.externalScopes.Push(state);
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                capture.Capture("category", category);
                capture.Capture("formatted", formatter(state, exception));
                capture.Capture("state", state);
                capture.Capture("exception", exception);
                if (exception is not null)
                {
                    capture.exceptions.Enqueue(exception);
                }
                capture.externalScopes.ForEachScope((scope, owner) => owner.Capture("scope", scope), capture);
            }
        }
    }

    private sealed class StreamingHttpMessageHandler(Stream body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
    }

    private sealed class DownloadBodyStream(ReadOnlyMemory<byte> content, Action? onEndOfStream = null, Action? onRead = null) : Stream
    {
        public int ReadCalls { get; private set; }
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            onRead?.Invoke();
            if (BytesRead == content.Length)
            {
                onEndOfStream?.Invoke();
                return 0;
            }

            var count = Math.Min(buffer.Length, content.Length - BytesRead);
            content.Span.Slice(BytesRead, count).CopyTo(buffer);
            BytesRead += count;
            return count;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Read(buffer.Span);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(count);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

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
