using System.Net;
using System.Text;

namespace Spectara.Revela.Plugin.Serve;

/// <summary>
/// Simple HTTP server for serving static files
/// </summary>
/// <remarks>
/// Uses .NET's built-in HttpListener - no external dependencies required.
/// </remarks>
public sealed class StaticFileServer : IDisposable
{
    private readonly string rootPath;
    private readonly HttpListener listener;
    private readonly Action<string, int>? requestCallback;
    private bool isRunning;
    private bool disposed;

    /// <summary>
    /// MIME type mappings for common static file extensions
    /// </summary>
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // HTML/Web
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".xml"] = "application/xml; charset=utf-8",

        // Images - Modern formats first
        [".avif"] = "image/avif",
        [".webp"] = "image/webp",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",

        // Fonts
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".eot"] = "application/vnd.ms-fontobject",

        // Other
        [".txt"] = "text/plain; charset=utf-8",
        [".md"] = "text/markdown; charset=utf-8",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".map"] = "application/json"
    };

    /// <summary>
    /// Creates a new static file server
    /// </summary>
    /// <param name="rootPath">Root directory to serve files from</param>
    /// <param name="port">Port number to listen on</param>
    /// <param name="requestCallback">Optional callback for each request (path, statusCode)</param>
    public StaticFileServer(string rootPath, int port, Action<string, int>? requestCallback = null)
    {
        this.rootPath = Path.GetFullPath(rootPath);
        this.requestCallback = requestCallback;
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
    }

    /// <summary>
    /// Start the HTTP server
    /// </summary>
    /// <exception cref="HttpListenerException">Thrown when port is already in use</exception>
    public void Start()
    {
        listener.Start();
        isRunning = true;

        // Start processing requests in background
        _ = Task.Run(ProcessRequestsAsync);
    }

    /// <summary>
    /// Stop the HTTP server gracefully
    /// </summary>
    public void Stop()
    {
        isRunning = false;
        listener.Stop();
    }

    /// <summary>
    /// Process incoming HTTP requests
    /// </summary>
    private async Task ProcessRequestsAsync()
    {
        while (isRunning)
        {
            try
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequest(context));
            }
            catch (HttpListenerException) when (!isRunning)
            {
                // Expected when stopping - listener.Stop() causes GetContextAsync to throw
                break;
            }
            catch (ObjectDisposedException) when (!isRunning)
            {
                // Expected when disposing
                break;
            }
        }
    }

    /// <summary>
    /// Handle a single HTTP request
    /// </summary>
    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Get requested path, default to index.html
            var urlPath = request.Url?.LocalPath ?? "/";
            if (urlPath.EndsWith('/'))
            {
                urlPath += "index.html";
            }

            // Security: Prevent directory traversal attacks
            var requestedPath = Path.GetFullPath(Path.Combine(rootPath, urlPath.TrimStart('/')));
            if (!requestedPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                SendError(response, 403, "Forbidden");
                requestCallback?.Invoke(urlPath, 403);
                return;
            }

            // Check if file exists
            if (!File.Exists(requestedPath))
            {
                SendError(response, 404, "Not Found");
                requestCallback?.Invoke(urlPath, 404);
                return;
            }

            // Serve the file
            var extension = Path.GetExtension(requestedPath);
            var contentType = GetMimeType(extension);
            var content = File.ReadAllBytes(requestedPath);

            response.ContentType = contentType;
            response.ContentLength64 = content.Length;
            response.StatusCode = 200;

            // Add cache headers for assets (not HTML)
            if (!extension.Equals(".html", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
            {
                response.Headers.Add("Cache-Control", "public, max-age=3600");
            }

            response.OutputStream.Write(content, 0, content.Length);
            requestCallback?.Invoke(urlPath, 200);
        }
        catch (Exception)
        {
            SendError(response, 500, "Internal Server Error");
            requestCallback?.Invoke(request.Url?.LocalPath ?? "/", 500);
        }
        finally
        {
            response.Close();
        }
    }

    /// <summary>
    /// Send an error response
    /// </summary>
    private static void SendError(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "text/plain; charset=utf-8";
        var content = Encoding.UTF8.GetBytes(message);
        response.ContentLength64 = content.Length;
        response.OutputStream.Write(content, 0, content.Length);
    }

    /// <summary>
    /// Get MIME type for a file extension
    /// </summary>
    /// <param name="extension">File extension including the dot (e.g., ".html")</param>
    /// <returns>MIME type string</returns>
    public static string GetMimeType(string extension)
    {
        return MimeTypes.TryGetValue(extension, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        Stop();
        listener.Close();
    }
}
