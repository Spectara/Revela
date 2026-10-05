using System.Globalization;
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
    /// Maximum accepted size of an iCal feed (10 MB). Real booking/holiday feeds are a few
    /// hundred KB at most; the cap stops a misbehaving or hostile server from filling the disk.
    /// </summary>
    internal const long MaxFeedBytes = 10 * 1024 * 1024;

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
        if (response.Content.Headers.ContentLength > MaxFeedBytes)
        {
            throw FeedTooLarge(uri.Host);
        }

        var destination = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            long bytesWritten;
            await using (var fileStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await using var limitedStream = new SizeLimitedWriteStream(fileStream, MaxFeedBytes);
                try
                {
                    await response.Content.CopyToAsync(limitedStream, timeout.Token);
                }
                catch (Exception ex) when (limitedStream.LimitExceeded && ex is not OperationCanceledException)
                {
                    // HttpContent may wrap the IOException thrown by the stream; report the limit instead.
                    throw FeedTooLarge(uri.Host);
                }

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

    private HttpRequestException FeedTooLarge(string host)
    {
        LogFeedTooLarge(host, MaxFeedBytes);
        return new HttpRequestException(
            HttpRequestError.ConfigurationLimitExceeded,
            string.Create(CultureInfo.InvariantCulture, $"The iCal feed exceeds the {MaxFeedBytes / (1024 * 1024)} MB size limit; the download was aborted."));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching iCal feed from {Host}")]
    private partial void LogFetchingHost(string host);

    [LoggerMessage(Level = LogLevel.Warning, Message = "iCal feed from {Host} exceeds the size limit of {LimitBytes} bytes; download aborted and the previous file retained")]
    private partial void LogFeedTooLarge(string host, long limitBytes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved iCal feed to {Path} ({Bytes} bytes)")]
    private partial void LogFetched(string path, long bytes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove temporary calendar download. The previous output file was retained; inspect temporary files in its directory.")]
    private partial void LogTemporaryCleanupFailed();

    /// <summary>
    /// Write-only pass-through that fails once more than <paramref name="maxBytes"/> are written.
    /// Bounding the destination keeps <see cref="HttpContent.CopyToAsync(Stream, CancellationToken)"/>
    /// streaming (no buffering) for any content implementation. Does not dispose <paramref name="inner"/>.
    /// </summary>
    private sealed class SizeLimitedWriteStream(Stream inner, long maxBytes) : Stream
    {
        private long written;

        public bool LimitExceeded { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Reserve(buffer.Length);
            inner.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reserve(buffer.Length);
            return inner.WriteAsync(buffer, cancellationToken);
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        private void Reserve(int count)
        {
            if (written + count > maxBytes)
            {
                LimitExceeded = true;
                throw new IOException("Size limit exceeded.");
            }

            written += count;
        }
    }
}
