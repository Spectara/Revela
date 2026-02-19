using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using System.Text;

namespace Spectara.Revela.Plugin.Serve;

/// <summary>
/// Simple HTTP server for serving static files
/// </summary>
/// <remarks>
/// Uses .NET's built-in HttpListener - no external dependencies required.
/// Implements <see cref="IAsyncDisposable"/> for graceful shutdown of background tasks.
/// </remarks>
internal sealed class StaticFileServer : IAsyncDisposable, IDisposable
{
    private readonly string rootPath;
    private readonly HttpListener listener;
    private readonly Action<string, int>? requestCallback;
    private CancellationTokenSource? cts;
    private Task? processingTask;
    private int disposed;

    /// <summary>
    /// MIME type mappings for common static file extensions
    /// </summary>
    private static readonly FrozenDictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

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
        listener.Prefixes.Add(string.Format(CultureInfo.InvariantCulture, "http://localhost:{0}/", port));
    }

    /// <summary>
    /// Start the HTTP server
    /// </summary>
    /// <exception cref="HttpListenerException">Thrown when port is already in use</exception>
    public void Start()
    {
        cts = new CancellationTokenSource();
        listener.Start();

        // Start processing requests in background — awaited in DisposeAsync
        processingTask = Task.Run(ProcessRequestsAsync);
    }

    /// <summary>
    /// Stop the HTTP server gracefully
    /// </summary>
    public void Stop()
    {
        if (cts is null or { IsCancellationRequested: true })
        {
            return;
        }

        cts.Cancel();

        try
        {
            listener.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed - ignore
        }
    }

    /// <summary>
    /// Process incoming HTTP requests
    /// </summary>
    private async Task ProcessRequestsAsync()
    {
        var token = cts?.Token ?? CancellationToken.None;

        while (cts is { IsCancellationRequested: false })
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = HandleRequestAsync(context, token);
            }
            catch (HttpListenerException) when (cts is null or { IsCancellationRequested: true })
            {
                // Expected when stopping - listener.Stop() causes GetContextAsync to throw
                break;
            }
            catch (ObjectDisposedException) when (cts is null or { IsCancellationRequested: true })
            {
                // Expected when disposing
                break;
            }
        }
    }

    /// <summary>
    /// Handle a single HTTP request
    /// </summary>
    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
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

            // Serve the file with async streaming
            var extension = Path.GetExtension(requestedPath);
            var contentType = GetMimeType(extension);

            response.ContentType = contentType;
            response.StatusCode = 200;

            // Add cache headers for assets (not HTML)
            if (!extension.Equals(".html", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
            {
                response.Headers.Add("Cache-Control", "public, max-age=3600");
            }

            await using var fileStream = new FileStream(
                requestedPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 65536, useAsync: true);
            response.ContentLength64 = fileStream.Length;
            await fileStream.CopyToAsync(response.OutputStream, cancellationToken);

            requestCallback?.Invoke(urlPath, 200);
        }
        catch (OperationCanceledException)
        {
            // Server shutting down during file transfer — expected
        }
        catch (Exception)
        {
            try
            {
                SendError(response, 500, "Internal Server Error");
            }
            catch (ObjectDisposedException)
            {
                // Response already closed
            }

            requestCallback?.Invoke(request.Url?.LocalPath ?? "/", 500);
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (ObjectDisposedException)
            {
                // Already closed
            }
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
        response.OutputStream.Write(content);
    }

    /// <summary>
    /// Get MIME type for a file extension
    /// </summary>
    /// <param name="extension">File extension including the dot (e.g., ".html")</param>
    /// <returns>MIME type string</returns>
    public static string GetMimeType(string extension) =>
        MimeTypes.TryGetValue(extension, out var mimeType)
            ? mimeType
            : "application/octet-stream";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) is 1)
        {
            return;
        }

        Stop();

        // Await background task for clean shutdown
        if (processingTask is not null)
        {
            await processingTask;
        }

        cts?.Dispose();

        try
        {
            listener.Close();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed - ignore
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) is 1)
        {
            return;
        }

        Stop();
        processingTask?.Wait(TimeSpan.FromSeconds(2));
        cts?.Dispose();

        try
        {
            listener.Close();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed - ignore
        }
    }
}
