using System.Net;

using Microsoft.Extensions.Logging.Abstractions;

using Spectara.Revela.Plugins.Source.Calendar.Services;

namespace Spectara.Revela.Tests.Plugins.Source.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class ICalFetcherTests : IDisposable
{
    private const string SampleIcal = """
        BEGIN:VCALENDAR
        VERSION:2.0
        BEGIN:VEVENT
        DTSTART;VALUE=DATE:20260320
        DTEND;VALUE=DATE:20260322
        UID:test@example.com
        SUMMARY:Test booking
        END:VEVENT
        END:VCALENDAR
        """;

    private readonly string tempDir = Path.Combine(Path.GetTempPath(), $"revela-test-{Guid.NewGuid():N}");

    [TestMethod]
    public async Task FetchAsync_Success_WritesFile()
    {
        // Arrange
        using var handler = new MockHttpMessageHandler(SampleIcal, HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var fetcher = new ICalFetcher(httpClient, NullLogger<ICalFetcher>.Instance);
        var outputPath = Path.Combine(tempDir, "test", "bookings.ics");

        // Act
        var bytes = await fetcher.FetchAsync("https://example.com/calendar.ics", outputPath);

        // Assert
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(bytes > 0);
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.IsTrue(content.Contains("BEGIN:VCALENDAR", StringComparison.Ordinal));
        Assert.IsTrue(content.Contains("DTSTART", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FetchAsync_HttpError_ThrowsException()
    {
        // Arrange
        using var handler = new MockHttpMessageHandler("Not Found", HttpStatusCode.NotFound);
        using var httpClient = new HttpClient(handler);
        var fetcher = new ICalFetcher(httpClient, NullLogger<ICalFetcher>.Instance);
        var outputPath = Path.Combine(tempDir, "should-not-exist.ics");

        // Act & Assert
        await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => fetcher.FetchAsync("https://example.com/missing.ics", outputPath));

        Assert.IsFalse(File.Exists(outputPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FetchAsync_InterruptedBody_PreservesExistingFileAndRemovesTemporaryFile(bool cancel)
    {
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "bookings.ics");
        await File.WriteAllTextAsync(outputPath, SampleIcal);
        using var handler = new InterruptedBodyHandler(cancel);
        using var client = new HttpClient(handler);
        var fetcher = new ICalFetcher(client, NullLogger<ICalFetcher>.Instance);

        if (cancel)
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => fetcher.FetchAsync("https://example.com/calendar.ics", outputPath));
        }
        else
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => fetcher.FetchAsync("https://example.com/calendar.ics", outputPath));
        }

        Assert.AreEqual(SampleIcal, await File.ReadAllTextAsync(outputPath));
        Assert.HasCount(1, Directory.GetFiles(tempDir));
    }

    [TestMethod]
    public async Task FetchAsync_Success_ReplacesExistingFileWithoutSidecars()
    {
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "bookings.ics");
        await File.WriteAllTextAsync(outputPath, "previous data");
        using var handler = new MockHttpMessageHandler(SampleIcal, HttpStatusCode.OK);
        using var client = new HttpClient(handler);

        var length = await new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("https://example.com/calendar.ics", outputPath);

        Assert.AreEqual(SampleIcal, await File.ReadAllTextAsync(outputPath));
        Assert.AreEqual(new FileInfo(outputPath).Length, length);
        Assert.HasCount(1, Directory.GetFiles(tempDir));
    }

    [TestMethod]
    public async Task FetchAsync_UnsafeAddress_DoesNotExposeCredential()
    {
        using var handler = new MockHttpMessageHandler(SampleIcal, HttpStatusCode.OK);
        using var client = new HttpClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("http://127.0.0.1/private-token", Path.Combine(tempDir, "bookings.ics")));

        Assert.DoesNotContain("private-token", exception.Message, StringComparison.Ordinal);
        Assert.IsFalse(Directory.Exists(tempDir));
    }

    [TestMethod]
    [DataRow("http://127.0.0.1/private-token")]
    [DataRow("http://example.com/private-token")]
    [DataRow("https://169.254.169.254/private-token")]
    public async Task FetchAsync_UnsafeRedirect_DoesNotRequestTargetOrReplaceFile(string target)
    {
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "bookings.ics");
        await File.WriteAllTextAsync(outputPath, SampleIcal);
        using var handler = new RedirectHandler(target);
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("https://example.com/calendar.ics", outputPath));

        Assert.HasCount(1, handler.Requests);
        Assert.DoesNotContain("private-token", error.Message, StringComparison.Ordinal);
        Assert.AreEqual(SampleIcal, await File.ReadAllTextAsync(outputPath));
        Assert.HasCount(1, Directory.GetFiles(tempDir));
    }

    [TestMethod]
    public async Task FetchAsync_CallerCancelsDuringBody_PreservesExistingFile()
    {
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "bookings.ics");
        await File.WriteAllTextAsync(outputPath, SampleIcal);
        using var cancellation = new CancellationTokenSource();
        using var handler = new CancelingBodyHandler(cancellation);
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("https://example.com/calendar.ics", outputPath, cancellation.Token));

        Assert.AreEqual(SampleIcal, await File.ReadAllTextAsync(outputPath));
        Assert.HasCount(1, Directory.GetFiles(tempDir));
        Assert.IsTrue(handler.Content.TokenObserved);
    }

    [TestMethod]
    public async Task FetchAsync_RelativeRedirect_ResolvesAndDownloads()
    {
        using var handler = new RedirectHandler("/calendar-final.ics");
        using var client = new HttpClient(handler);
        var outputPath = Path.Combine(tempDir, "bookings.ics");

        await new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("https://example.com/start", outputPath);

        Assert.HasCount(2, handler.Requests);
        Assert.AreEqual(new Uri("https://example.com/calendar-final.ics"), handler.Requests[1]);
        Assert.AreEqual(SampleIcal, await File.ReadAllTextAsync(outputPath));
    }

    [TestMethod]
    public async Task FetchAsync_RedirectLoop_IsBounded()
    {
        using var handler = new RedirectHandler("/loop", repeat: true);
        using var client = new HttpClient(handler);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("https://example.com/start", Path.Combine(tempDir, "bookings.ics")));

        Assert.HasCount(6, handler.Requests);
        Assert.IsFalse(Directory.Exists(tempDir));
    }

    [TestMethod]
    public async Task FetchAsync_CancellationAndCleanupFailure_PreservesCancellation()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows read-only file deletion semantics are required for this cleanup failure fixture.");
        }

        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "bookings.ics");
        await File.WriteAllTextAsync(outputPath, SampleIcal);
        using var cancellation = new CancellationTokenSource();
        using var handler = new CancelingBodyHandler(cancellation, blockCleanup: true);
        using var client = new HttpClient(handler);
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("https://example.com/calendar.ics", outputPath, cancellation.Token));

            Assert.AreEqual(SampleIcal, await File.ReadAllTextAsync(outputPath));
            Assert.HasCount(1, Directory.GetFiles(tempDir, "*.tmp"));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(tempDir, "*.tmp"))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }
    }

    [TestMethod]
    public async Task FetchAsync_BodyDeadlineExpires_PreservesExistingFile()
    {
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "bookings.ics");
        await File.WriteAllTextAsync(outputPath, SampleIcal);
        using var handler = new StalledBodyHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(100) };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("https://example.com/calendar.ics", outputPath));

        Assert.IsTrue(handler.Content.Started);
        Assert.AreEqual(SampleIcal, await File.ReadAllTextAsync(outputPath));
        Assert.HasCount(1, Directory.GetFiles(tempDir));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("http://[private-token")]
    public async Task FetchAsync_InvalidRedirectLocation_FailsWithoutExposingValue(string? location)
    {
        using var handler = new InvalidRedirectHandler(location);
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            new ICalFetcher(client, NullLogger<ICalFetcher>.Instance).FetchAsync("https://example.com/start", Path.Combine(tempDir, "bookings.ics")));

        Assert.DoesNotContain("private-token", error.ToString(), StringComparison.Ordinal);
        Assert.IsFalse(Directory.Exists(tempDir));
    }

    [TestMethod]
    public async Task FetchAsync_CreatesDirectories()
    {
        // Arrange
        using var handler = new MockHttpMessageHandler(SampleIcal, HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var fetcher = new ICalFetcher(httpClient, NullLogger<ICalFetcher>.Instance);
        var outputPath = Path.Combine(tempDir, "deep", "nested", "path", "bookings.ics");

        // Act
        await fetcher.FetchAsync("https://example.com/calendar.ics", outputPath);

        // Assert
        Assert.IsTrue(File.Exists(outputPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Simple mock handler that returns a fixed response.
    /// </summary>
    private sealed class MockHttpMessageHandler(string content, HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }

    private sealed class InterruptedBodyHandler(bool cancel) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new InterruptedContent(cancel) });
    }

    private sealed class RedirectHandler(string target, bool repeat = false) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var response = new HttpResponseMessage(Requests.Count == 1 || repeat ? HttpStatusCode.Redirect : HttpStatusCode.OK)
            {
                Content = new StringContent(SampleIcal)
            };
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                response.Headers.Location = new Uri(target, UriKind.RelativeOrAbsolute);
            }

            return Task.FromResult(response);
        }
    }

    private sealed class InterruptedContent(bool cancel) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync("partial download"u8.ToArray());
            if (cancel)
            {
                throw new OperationCanceledException();
            }

            throw new HttpRequestException("Download interrupted");
        }
    }

    private sealed class CancelingBodyHandler(CancellationTokenSource source, bool blockCleanup = false) : HttpMessageHandler
    {
        public CancelingContent Content { get; } = new(source, blockCleanup);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = Content });
    }

    private sealed class CancelingContent(CancellationTokenSource source, bool blockCleanup) : HttpContent
    {
        public bool TokenObserved { get; private set; }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("A cancellation token must be supplied.");

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await stream.WriteAsync("partial"u8.ToArray(), cancellationToken);
            if (blockCleanup)
            {
                File.SetAttributes(((FileStream)stream).Name, FileAttributes.ReadOnly);
            }

            await source.CancelAsync();
            TokenObserved = cancellationToken.IsCancellationRequested;
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class InvalidRedirectHandler(string? location) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            if (location is not null)
            {
                response.Headers.TryAddWithoutValidation("Location", location);
            }

            return Task.FromResult(response);
        }
    }

    private sealed class StalledBodyHandler : HttpMessageHandler
    {
        public StalledContent Content { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = Content });
    }

    private sealed class StalledContent : HttpContent
    {
        public bool Started { get; private set; }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("A cancellation token must be supplied.");

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await stream.WriteAsync("partial"u8.ToArray(), cancellationToken);
            Started = true;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
