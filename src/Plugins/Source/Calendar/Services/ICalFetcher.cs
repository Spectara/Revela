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
                $"iCal URL '{url}' is not a safe outbound target. " +
                "URLs must use http(s) and not point to loopback, private, or link-local addresses.");
        }

        // Information: host only — iCal feed URLs frequently embed auth tokens in query strings
        // (Booking.com, Airbnb personal feeds, etc.). Full URL stays at Debug for diagnostics.
        LogFetchingHost(uri.Host);
        LogFetchingUrl(url);

        using var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var directory = Path.GetDirectoryName(outputPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        long bytesWritten;
        await using (var fileStream = File.Create(outputPath))
        {
            await response.Content.CopyToAsync(fileStream, cancellationToken);
            bytesWritten = fileStream.Length;
        }

        LogFetched(outputPath, bytesWritten);

        return bytesWritten;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching iCal feed from {Host}")]
    private partial void LogFetchingHost(string host);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching iCal feed: {Url}")]
    private partial void LogFetchingUrl(string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved iCal feed to {Path} ({Bytes} bytes)")]
    private partial void LogFetched(string path, long bytes);
}
