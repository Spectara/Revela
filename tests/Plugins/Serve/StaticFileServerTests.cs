using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Spectara.Revela.Plugins.Serve;

namespace Spectara.Revela.Tests.Plugins.Serve;

[TestClass]
[TestCategory("Unit")]
public sealed class StaticFileServerTests
{
    private static readonly string[] AllowedMethods = ["GET", "HEAD"];

    /// <summary>
    /// Build a URI for the local test server
    /// </summary>
    private static Uri LocalUri(int port, string path = "/") =>
        new($"http://localhost:{port}{path}");
    [TestMethod]
    [DataRow(".html", "text/html; charset=utf-8")]
    [DataRow(".HTML", "text/html; charset=utf-8")]
    [DataRow(".css", "text/css; charset=utf-8")]
    [DataRow(".js", "text/javascript; charset=utf-8")]
    [DataRow(".json", "application/json; charset=utf-8")]
    [DataRow(".avif", "image/avif")]
    [DataRow(".webp", "image/webp")]
    [DataRow(".jpg", "image/jpeg")]
    [DataRow(".jpeg", "image/jpeg")]
    [DataRow(".png", "image/png")]
    [DataRow(".svg", "image/svg+xml")]
    [DataRow(".ico", "image/x-icon")]
    [DataRow(".woff2", "font/woff2")]
    [DataRow(".unknown", "application/octet-stream")]
    public void GetMimeType_ReturnsCorrectType(string extension, string expectedMimeType)
    {
        // Act
        var result = StaticFileServer.GetMimeType(extension);

        // Assert
        Assert.AreEqual(expectedMimeType, result);
    }

    [TestMethod]
    public void GetMimeType_IsCaseInsensitive()
    {
        // Arrange & Act
        var lowercase = StaticFileServer.GetMimeType(".html");
        var uppercase = StaticFileServer.GetMimeType(".HTML");
        var mixed = StaticFileServer.GetMimeType(".HtMl");

        // Assert
        Assert.AreEqual(lowercase, uppercase);
        Assert.AreEqual(lowercase, mixed);
    }

    [TestMethod]
    public void GetMimeType_UnknownExtension_ReturnsOctetStream()
    {
        // Act
        var result = StaticFileServer.GetMimeType(".xyz123");

        // Assert
        Assert.AreEqual("application/octet-stream", result);
    }

    [TestMethod]
    public void GetMimeType_EmptyExtension_ReturnsOctetStream()
    {
        // Act
        var result = StaticFileServer.GetMimeType("");

        // Assert
        Assert.AreEqual("application/octet-stream", result);
    }

    [TestMethod]
    public void GetMimeType_AllMimeTypes_AreRegistered()
    {
        // Verify all expected extensions have MIME types (not octet-stream)
        var expectedExtensions = new[]
        {
            ".html", ".htm", ".css", ".js", ".mjs", ".json", ".xml",
            ".avif", ".webp", ".jpg", ".jpeg", ".png", ".gif", ".svg", ".ico",
            ".woff", ".woff2", ".ttf", ".otf", ".eot",
            ".txt", ".md", ".pdf", ".zip", ".map"
        };

        foreach (var ext in expectedExtensions)
        {
            var mimeType = StaticFileServer.GetMimeType(ext);
            Assert.AreNotEqual("application/octet-stream", mimeType, $"Extension '{ext}' should have a registered MIME type");
        }
    }

    // ── TryResolveSafePath: directory traversal protection ────────────────

    [TestMethod]
    public void TryResolveSafePath_PlainFile_IsAllowed()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rev-serve-root"));

        var allowed = StaticFileServer.TryResolveSafePath(root, "/index.html", out var resolved);

        Assert.IsTrue(allowed);
        Assert.AreEqual(Path.Combine(root, "index.html"), resolved);
    }

    [TestMethod]
    public void TryResolveSafePath_NestedFile_IsAllowed()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rev-serve-root"));

        var allowed = StaticFileServer.TryResolveSafePath(root, "/sub/dir/page.html", out _);

        Assert.IsTrue(allowed);
    }

    [TestMethod]
    public void TryResolveSafePath_ParentTraversal_IsRejected()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rev-serve-root"));

        var allowed = StaticFileServer.TryResolveSafePath(root, "/../etc/passwd", out _);

        Assert.IsFalse(allowed);
    }

    [TestMethod]
    public void TryResolveSafePath_SiblingDirectoryWithSharedPrefix_IsRejected()
    {
        // The historical bug: rootPath = "<tmp>\site" and a sibling "<tmp>\siteother\"
        // would pass a naive StartsWith(rootPath, …) check.
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rev-serve-site"));

        var allowed = StaticFileServer.TryResolveSafePath(root, "/../rev-serve-siteother/secret.txt", out _);

        Assert.IsFalse(allowed);
    }

    [TestMethod]
    public void TryResolveSafePath_DeepTraversalEscape_IsRejected()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rev-serve-root"));

        var allowed = StaticFileServer.TryResolveSafePath(root, "/../../../../../../../../etc/passwd", out _);

        Assert.IsFalse(allowed);
    }

    [TestMethod]
    public void TryResolveSafePath_RootPath_IsAllowed()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rev-serve-root"));

        var allowed = StaticFileServer.TryResolveSafePath(root, "/", out var resolved);

        Assert.IsTrue(allowed);
        Assert.AreEqual(root, resolved);
    }

    [TestMethod]
    public void TryResolveSafePath_TraversalThatLandsBackInsideRoot_IsAllowed()
    {
        // /sub/../index.html resolves back to /index.html, which is inside root.
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rev-serve-root"));

        var allowed = StaticFileServer.TryResolveSafePath(root, "/sub/../index.html", out var resolved);

        Assert.IsTrue(allowed);
        Assert.AreEqual(Path.Combine(root, "index.html"), resolved);
    }

    [TestMethod]
    [DataRow("br, gzip", ".br", "br")]
    [DataRow("gzip, br;q=0.5", ".gz", "gzip")]
    [DataRow("br;q=0, gzip", ".gz", "gzip")]
    [DataRow("*;q=0.5", ".br", "br")]
    [DataRow("identity", "", null)]
    public void ResolveEncodedPath_AcceptEncoding_SelectsBestAvailableVariant(
        string acceptEncoding,
        string expectedSuffix,
        string? expectedEncoding)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"revela-encoding-{Guid.NewGuid():N}.html");
        File.WriteAllText(tempFile, "content");
        File.WriteAllText(tempFile + ".gz", "gzip");
        File.WriteAllText(tempFile + ".br", "brotli");

        try
        {
            var (path, encoding) = StaticFileServer.ResolveEncodedPath(tempFile, acceptEncoding);

            Assert.AreEqual(tempFile + expectedSuffix, path);
            Assert.AreEqual(expectedEncoding, encoding);
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(tempFile + ".gz");
            File.Delete(tempFile + ".br");
        }
    }

    [TestMethod]
    public void ResolveEncodedPath_PrecompressedVariantIsOlderThanOriginal_ReturnsOriginal()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"revela-encoding-{Guid.NewGuid():N}.css");
        var compressedFile = tempFile + ".br";
        File.WriteAllText(tempFile, "current");
        File.WriteAllText(compressedFile, "stale");
        var currentTimestamp = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(compressedFile, currentTimestamp.AddMinutes(-1));
        File.SetLastWriteTimeUtc(tempFile, currentTimestamp);

        try
        {
            var (path, encoding) = StaticFileServer.ResolveEncodedPath(tempFile, "br");

            Assert.AreEqual(tempFile, path);
            Assert.IsNull(encoding);
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(compressedFile);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_PrecompressedBrotli_ReturnsEncodedContentWithOriginalMimeType()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "style.css");
        const string expectedContent = "body { color: red; }";
        await File.WriteAllTextAsync(filePath, expectedContent);
        await WriteCompressedAsync(filePath + ".br", expectedContent, useBrotli: true);

        try
        {
            await using var server = StartServer(tempDir, out var port);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, LocalUri(port, "/style.css"));
            request.Headers.AcceptEncoding.ParseAdd("br");

            using var response = await client.SendAsync(request);
            await using var compressed = await response.Content.ReadAsStreamAsync();
            await using var brotli = new BrotliStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(brotli);
            var content = await reader.ReadToEndAsync();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.Contains(response.Content.Headers.ContentEncoding.ToList(), "br");
            CollectionAssert.Contains(response.Headers.Vary.ToList(), "Accept-Encoding");
            Assert.AreEqual("text/css; charset=utf-8", response.Content.Headers.ContentType?.ToString());
            Assert.AreEqual(expectedContent, content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_HeadRequest_ReturnsEncodedHeadersWithoutBody()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "index.html");
        const string content = "<html><body>Index</body></html>";
        await File.WriteAllTextAsync(filePath, content);
        await WriteCompressedAsync(filePath + ".gz", content, useBrotli: false);

        try
        {
            await using var server = StartServer(tempDir, out var port);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Head, LocalUri(port));
            request.Headers.AcceptEncoding.ParseAdd("gzip");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsByteArrayAsync();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.Contains(response.Content.Headers.ContentEncoding.ToList(), "gzip");
            CollectionAssert.Contains(response.Headers.Vary.ToList(), "Accept-Encoding");
            Assert.AreEqual(new FileInfo(filePath + ".gz").Length, response.Content.Headers.ContentLength);
            Assert.IsEmpty(body);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_HeadRequestForMissingFile_Returns404WithoutBody()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await using var server = StartServer(tempDir, out var port);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Head, LocalUri(port, "/missing.html"));

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsByteArrayAsync();

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.IsEmpty(body);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_UnsupportedMethod_Returns405WithAllowHeader()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "index.html"), "content");

        try
        {
            await using var server = StartServer(tempDir, out var port);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, LocalUri(port));

            using var response = await client.SendAsync(request);

            Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            CollectionAssert.AreEquivalent(AllowedMethods, response.Content.Headers.Allow.ToList());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static async Task WriteCompressedAsync(string path, string content, bool useBrotli)
    {
        await using var output = File.Create(path);
        await using Stream compression = useBrotli
            ? new BrotliStream(output, CompressionLevel.SmallestSize)
            : new GZipStream(output, CompressionLevel.SmallestSize);
        await using var writer = new StreamWriter(compression);
        await writer.WriteAsync(content);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_ServesStaticFile_ReturnsCorrectContent()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var expectedContent = "<html><body>Hello World</body></html>";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "index.html"), expectedContent);

            await using var server = StartServer(tempDir, out var port);

            using var client = new HttpClient();

            // Act
            using var response = await client.GetAsync(LocalUri(port, "/index.html"));
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(expectedContent, content);
            Assert.AreEqual("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_NonExistentFile_Returns404()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var callbackInvoked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var server = StartServer(tempDir, out var port, (_, status) => callbackInvoked.TrySetResult(status));

            using var client = new HttpClient();

            // Act
            using var response = await client.GetAsync(LocalUri(port, "/missing.html"));

            // Assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());
            Assert.AreEqual("Not Found", await response.Content.ReadAsStringAsync());
            Assert.AreEqual(404, await callbackInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_MissingFileWithNotFoundPage_Returns404WithPageBody()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        const string notFoundPage = "<!DOCTYPE html><title>Seite nicht gefunden</title>";
        await File.WriteAllTextAsync(Path.Combine(tempDir, "404.html"), notFoundPage);

        try
        {
            var callbackInvoked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var server = StartServer(tempDir, out var port, (_, status) => callbackInvoked.TrySetResult(status));
            using var client = new HttpClient();

            using var response = await client.GetAsync(LocalUri(port, "/gallery/missing/"));
            var content = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual(notFoundPage, content);
            Assert.AreEqual("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
            Assert.AreEqual(404, await callbackInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_HeadMissingFileWithNotFoundPage_Returns404HtmlHeadersWithoutBody()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "404.html"), "<!DOCTYPE html><title>404</title>");

        try
        {
            await using var server = StartServer(tempDir, out var port);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Head, LocalUri(port, "/missing.html"));

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsByteArrayAsync();

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
            Assert.IsEmpty(body);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_EncodedTraversalWithNotFoundPage_StaysForbidden()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        var rootDir = Path.Combine(tempDir, "output");
        Directory.CreateDirectory(rootDir);
        await File.WriteAllTextAsync(Path.Combine(rootDir, "404.html"), "NOT-FOUND-PAGE");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "secret.txt"), "SECRET");

        try
        {
            await using var server = StartServer(rootDir, out var port);

            // Raw request: HttpClient would normalize the dot segments before sending.
            var response = await SendRawGetAsync(port, "/..%5Csecret.txt");

            Assert.StartsWith("HTTP/1.1 403", response, response);
            Assert.DoesNotContain("SECRET", response, StringComparison.Ordinal);
            Assert.DoesNotContain("NOT-FOUND-PAGE", response, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static async Task<string> SendRawGetAsync(int port, string rawPath)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = tcp.GetStream();
        var request = $"GET {rawPath} HTTP/1.1\r\nHost: localhost:{port}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(request));
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_DirectoryTraversal_Returns403()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await using var server = StartServer(tempDir, out var port);

            using var client = new HttpClient();

            // Act — attempt directory traversal
            using var response = await client.GetAsync(LocalUri(port, "/../../../etc/passwd"));

            // Assert — should either be 403 (traversal detected) or 404 (file doesn't exist)
            Assert.IsTrue(
                response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"Expected 403 or 404, got {response.StatusCode}");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_RootPath_ServesIndexHtml()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var expectedContent = "<html><body>Index</body></html>";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "index.html"), expectedContent);

            await using var server = StartServer(tempDir, out var port);

            using var client = new HttpClient();

            // Act — request root path
            using var response = await client.GetAsync(LocalUri(port));
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(expectedContent, content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_AssetFile_HasCacheHeaders()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "style.css"), "body { color: red; }");

            await using var server = StartServer(tempDir, out var port);

            using var client = new HttpClient();

            // Act
            using var response = await client.GetAsync(LocalUri(port, "/style.css"));

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("public, max-age=3600", response.Headers.CacheControl?.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_HtmlFile_NoCacheHeaders()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "page.html"), "<html></html>");

            await using var server = StartServer(tempDir, out var port);

            using var client = new HttpClient();

            // Act
            using var response = await client.GetAsync(LocalUri(port, "/page.html"));

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNull(response.Headers.CacheControl, "HTML files should not have Cache-Control headers");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Server_StopAndDispose_DoesNotThrow()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var server = StartServer(tempDir, out _);

            // Act & Assert — should not throw
            server.Stop();
            server.Stop(); // Double stop should be safe
            server.Dispose();
            server.Dispose(); // Double dispose should be safe
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_RequestCallback_IsInvoked()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "test.txt"), "hello");

            var callbackInvoked = new TaskCompletionSource<(string path, int status)>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var server = StartServer(tempDir, out var port, (path, status) =>
                callbackInvoked.TrySetResult((path, status)));

            using var client = new HttpClient();

            // Act
            using var response = await client.GetAsync(LocalUri(port, "/test.txt"));

            // Assert — wait for callback with timeout
            var (callbackPath, callbackStatus) = await callbackInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("/test.txt", callbackPath);
            Assert.AreEqual(200, callbackStatus);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Server_DisposeWithActiveHandler_WaitsForFileRelease(bool synchronous)
    {
        var tempDir = Directory.CreateTempSubdirectory("revela-serve-dispose-").FullName;
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCallback = new ManualResetEventSlim();
        var synchronizationContext = new RecordingSynchronizationContext();

        try
        {
            var filePath = Path.Combine(tempDir, "large.bin");
            await File.WriteAllBytesAsync(filePath, new byte[16 * 1024 * 1024]);
            await using var server = StartServer(tempDir, out var port, (path, status) =>
            {
                if (path.Equals("/large.bin", StringComparison.Ordinal) && status is 200)
                {
                    callbackEntered.TrySetResult();
                    releaseCallback.Wait();
                }
            });
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var disposal = Task.CompletedTask;
            var repeatedDisposal = Task.CompletedTask;

            try
            {
                using var response = await client.GetAsync(LocalUri(port, "/large.bin"), HttpCompletionOption.ResponseHeadersRead);
                using var probe = await client.GetAsync(LocalUri(port, "/missing.html"));
                Assert.AreEqual(HttpStatusCode.NotFound, probe.StatusCode);
                Assert.IsFalse(callbackEntered.Task.IsCompleted, "The unread response must keep the file transfer active.");
                await response.Content.CopyToAsync(Stream.Null);
                await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.ThrowsExactly<IOException>(() => File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());

                disposal = synchronous
                    ? Task.Run(() =>
                    {
                        var previousContext = SynchronizationContext.Current;
                        try
                        {
                            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                            server.Dispose();
                        }
                        finally
                        {
                            SynchronizationContext.SetSynchronizationContext(previousContext);
                        }
                    })
                    : server.DisposeAsync().AsTask();
                repeatedDisposal = server.DisposeAsync().AsTask();

                await Assert.ThrowsExactlyAsync<TimeoutException>(
                    () => Task.WhenAny(disposal, repeatedDisposal).WaitAsync(TimeSpan.FromSeconds(3)),
                    "Disposal must remain pending while the callback holds the file stream open.");
            }
            finally
            {
                releaseCallback.Set();
                await Task.WhenAll(disposal, repeatedDisposal).WaitAsync(TimeSpan.FromSeconds(10));
            }

            Assert.AreEqual(0, synchronizationContext.PostCount, "Synchronous disposal must not capture the caller's context.");
            using var reopened = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.AreEqual(16 * 1024 * 1024, reopened.Length);
        }
        finally
        {
            releaseCallback.Set();
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Server_DisposeWithUnreadResponse_CancelsTransferAndReleasesFile(bool synchronous)
    {
        var tempDir = Directory.CreateTempSubdirectory("revela-serve-cancel-").FullName;
        try
        {
            var filePath = Path.Combine(tempDir, "large.bin");
            await File.WriteAllBytesAsync(filePath, new byte[16 * 1024 * 1024]);
            await using var server = StartServer(tempDir, out var port);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var response = await client.GetAsync(LocalUri(port, "/large.bin"), HttpCompletionOption.ResponseHeadersRead);
            using var probe = await client.GetAsync(LocalUri(port, "/missing.html"));
            Assert.AreEqual(HttpStatusCode.NotFound, probe.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.ThrowsExactly<IOException>(() => File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());

            var disposal = synchronous ? Task.Run(server.Dispose) : server.DisposeAsync().AsTask();
            await disposal.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => response.Content.CopyToAsync(Stream.Null).WaitAsync(TimeSpan.FromSeconds(10)));

            using var reopened = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.AreEqual(16 * 1024 * 1024, reopened.Length);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_StopAndRestart_ServesRequestsAndDisposesIdempotently()
    {
        var tempDir = Directory.CreateTempSubdirectory("revela-serve-restart-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "index.html"), "restart");
            await using var server = StartServer(tempDir, out var port);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var initialResponse = await client.GetAsync(LocalUri(port));
            Assert.AreEqual("restart", await initialResponse.Content.ReadAsStringAsync());

            server.Start();
            server.Stop();
            server.Stop();
            server.Start();
            using var restartedResponse = await client.GetAsync(LocalUri(port));
            Assert.AreEqual(HttpStatusCode.OK, restartedResponse.StatusCode);
            Assert.AreEqual("restart", await restartedResponse.Content.ReadAsStringAsync());

            await server.DisposeAsync();
            await server.DisposeAsync();
            server.Stop();
            Assert.ThrowsExactly<ObjectDisposedException>(server.Start);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Server_StopBeforeAccept_OnlyIgnoresInvalidOperationWhenLocalTokenIsCanceled(bool cancelLocalToken)
    {
        var tempDir = Directory.CreateTempSubdirectory("revela-serve-accept-").FullName;
        try
        {
            await using var server = StartServer(tempDir, out _);
            using var cancellation = new CancellationTokenSource();
            InvalidOperationException? acceptException = null;
            var processing = server.ProcessRequestsAsync(cancellation.Token, async listener =>
            {
                server.Stop();
                if (cancelLocalToken)
                {
                    await cancellation.CancelAsync();
                }

                try
                {
                    return await listener.GetContextAsync();
                }
                catch (InvalidOperationException exception)
                {
                    acceptException = exception;
                    throw;
                }
            });

            if (cancelLocalToken)
            {
                await processing.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.IsTrue(processing.IsCompletedSuccessfully);
            }
            else
            {
                var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => processing.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.AreSame(acceptException, exception);
            }

            Assert.IsNotNull(acceptException, "The stopped listener must throw at the accept boundary.");
            await server.DisposeAsync();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Server_StartWithBoundPrefix_ClosesFailedListenerWithoutAffectingOwner()
    {
        var tempDir = Directory.CreateTempSubdirectory("revela-serve-binding-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "index.html"), "owner");
            await using var server = StartServer(tempDir, out var port);
            await using var conflictingServer = new StaticFileServer(tempDir, port);
            var originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
            var exception = Assert.ThrowsExactly<HttpListenerException>(() => StartWithInvariantErrors(conflictingServer));
            Assert.AreSame(originalCulture, System.Globalization.CultureInfo.CurrentUICulture);
            Assert.IsTrue(IsBindCollision(exception, port), $"Unexpected binding error: {exception.NativeErrorCode}");
            Assert.ThrowsExactly<ObjectDisposedException>(conflictingServer.Start);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var response = await client.GetAsync(LocalUri(port));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("owner", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(400, "Bad Request")]
    [DataRow(400, "Invalid port in prefix.")]
    [DataRow(400, "Failed to listen on prefix 'http://localhost:49153/' because it conflicts with an existing registration on the machine.")]
    [DataRow(400, "Failed to listen on prefix 'http://localhost:49152/other/' because it conflicts with an existing registration on the machine.")]
    [DataRow(403, "Failed to listen on prefix 'http://localhost:49152/' because it conflicts with an existing registration on the machine.")]
    public void IsBindCollision_UnrelatedListenerError_ReturnsFalse(int errorCode, string message)
    {
        var exception = new HttpListenerException(errorCode, message);

        Assert.IsFalse(IsBindCollision(exception, 49152));
    }

    [TestMethod]
    public void IsBindCollision_ExactManagedPrefixConflict_RequiresUnixListener()
    {
        var exception = new HttpListenerException(400,
            "Failed to listen on prefix 'http://localhost:49152/' because it conflicts with an existing registration on the machine.");

        Assert.AreEqual(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), IsBindCollision(exception, 49152));
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int postCount;

        public int PostCount => Volatile.Read(ref postCount);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref postCount);
            base.Post(callback, state);
        }
    }

    private static StaticFileServer StartServer(string rootPath, out int port, Action<string, int>? requestCallback = null)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            port = RandomNumberGenerator.GetInt32(49152, 65536);
            var server = new StaticFileServer(rootPath, port, requestCallback);
            try
            {
                StartWithInvariantErrors(server);
                return server;
            }
            catch (HttpListenerException exception) when (IsBindCollision(exception, port))
            {
                server.Dispose();
            }
            catch
            {
                server.Dispose();
                throw;
            }
        }

        throw new InvalidOperationException("Could not bind a test server after 20 port collisions.");
    }

    private static void StartWithInvariantErrors(StaticFileServer server)
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
            server.Start();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private static bool IsBindCollision(HttpListenerException exception, int port) =>
        exception.NativeErrorCode is 183 or (int)SocketError.AddressAlreadyInUse ||
        (OperatingSystem.IsWindows() && exception.NativeErrorCode is 32) ||
        (OperatingSystem.IsLinux() && exception.NativeErrorCode is 98) ||
        (OperatingSystem.IsMacOS() && exception.NativeErrorCode is 48) ||
        ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) &&
            exception.NativeErrorCode is (int)HttpStatusCode.BadRequest &&
            exception.Message.Equals(FormattableString.Invariant(
                $"Failed to listen on prefix 'http://localhost:{port}/' because it conflicts with an existing registration on the machine."),
                StringComparison.Ordinal));
}
