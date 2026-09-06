using Spectara.Revela.Sdk.Validation;

namespace Spectara.Revela.Plugins.Source.Calendar.Services;

/// <summary>
/// Fetches iCal feeds via HTTP and saves them to the source directory.
/// </summary>
internal sealed partial class ICalFetcher(
    HttpClient httpClient,
    ILogger<ICalFetcher> logger)
{
    /// <summary>
    /// Fetches an iCal feed from a URL and saves it to the specified path.
    /// </summary>
    /// <param name="url">The iCal feed URL.</param>
    /// <param name="outputPath">Absolute path to write the .ics file to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of bytes written.</returns>
    public async Task<long> FetchAsync(string url, string outputPath, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !UrlSafety.IsSafeOutboundUrl(uri, allowHttp: true))
        {
            throw new InvalidOperationException(
                "The configured iCal URL is not a safe outbound target. " +
                "URLs must use http(s) and not point to loopback, private, or link-local addresses.");
        }

        LogFetchingHost(uri.Host);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(httpClient.Timeout);
        using var response = await GetResponseAsync(uri, timeout.Token);
        response.EnsureSuccessStatusCode();

        var destination = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            long bytesWritten;
            await using (var fileStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await response.Content.CopyToAsync(fileStream, timeout.Token);
                bytesWritten = fileStream.Length;
            }

            timeout.Token.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destination, overwrite: true);
            LogFetched(outputPath, bytesWritten);
            return bytesWritten;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogTemporaryCleanupFailed();
                }
            }
        }
    }

    private async Task<HttpResponseMessage> GetResponseAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var redirects = 0; ; redirects++)
        {
            var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))
            {
                return response;
            }

            using (response)
            {
                Uri? location;
                try
                {
                    location = response.Headers.Location;
                }
                catch (FormatException)
                {
                    throw new HttpRequestException("Calendar redirect contains an invalid location.");
                }
                if (redirects >= 5 || location is null || !Uri.TryCreate(uri, location, out var next) ||
                    !UrlSafety.IsSafeOutboundUrl(next, allowHttp: true) ||
                    (uri.Scheme == Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttps))
                {
                    throw new HttpRequestException("Calendar redirect is unsafe, invalid, or exceeds the redirect limit.");
                }

                uri = next;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching iCal feed from {Host}")]
    private partial void LogFetchingHost(string host);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved iCal feed to {Path} ({Bytes} bytes)")]
    private partial void LogFetched(string path, long bytes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove temporary calendar download. The previous output file was retained; inspect temporary files in its directory.")]
    private partial void LogTemporaryCleanupFailed();
}
