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
        LogFetching(url);

        using var response = await httpClient.GetAsync(new Uri(url), cancellationToken);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching iCal feed: {Url}")]
    private partial void LogFetching(string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved iCal feed to {Path} ({Bytes} bytes)")]
    private partial void LogFetched(string path, long bytes);
}
