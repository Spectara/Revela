using System.Net;
using System.Net.Sockets;

namespace Spectara.Revela.Plugin.Serve.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class StaticFileServerTests
{
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

            var port = GetAvailablePort();
            using var server = new StaticFileServer(tempDir, port);
            server.Start();

            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync(LocalUri(port, "/index.html"));
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
            var port = GetAvailablePort();
            var callbackStatusCode = 0;

            using var server = new StaticFileServer(tempDir, port, (_, status) => callbackStatusCode = status);
            server.Start();

            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync(LocalUri(port, "/missing.html"));

            // Assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

            // Give callback a moment to execute
            await Task.Delay(50);
            Assert.AreEqual(404, callbackStatusCode);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
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
            var port = GetAvailablePort();
            var callbackStatusCode = 0;

            using var server = new StaticFileServer(tempDir, port, (_, status) => callbackStatusCode = status);
            server.Start();

            using var client = new HttpClient();

            // Act — attempt directory traversal
            var response = await client.GetAsync(LocalUri(port, "/../../../etc/passwd"));

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

            var port = GetAvailablePort();
            using var server = new StaticFileServer(tempDir, port);
            server.Start();

            using var client = new HttpClient();

            // Act — request root path
            var response = await client.GetAsync(LocalUri(port));
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

            var port = GetAvailablePort();
            using var server = new StaticFileServer(tempDir, port);
            server.Start();

            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync(LocalUri(port, "/style.css"));

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

            var port = GetAvailablePort();
            using var server = new StaticFileServer(tempDir, port);
            server.Start();

            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync(LocalUri(port, "/page.html"));

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
            var port = GetAvailablePort();
            var server = new StaticFileServer(tempDir, port);
            server.Start();

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
    public void Server_RequestCallback_IsInvoked()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"revela-serve-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello");

            var port = GetAvailablePort();
            var callbackInvoked = new TaskCompletionSource<(string path, int status)>();

            using var server = new StaticFileServer(tempDir, port, (path, status) =>
                callbackInvoked.TrySetResult((path, status)));
            server.Start();

            using var client = new HttpClient();

            // Act
            _ = client.GetAsync(LocalUri(port, "/test.txt"));

            // Assert — wait for callback with timeout
            var completed = callbackInvoked.Task.Wait(TimeSpan.FromSeconds(5));
            Assert.IsTrue(completed, "Request callback should have been invoked");

            var (callbackPath, callbackStatus) = callbackInvoked.Task.Result;
            Assert.AreEqual("/test.txt", callbackPath);
            Assert.AreEqual(200, callbackStatus);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Find an available TCP port for testing
    /// </summary>
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
