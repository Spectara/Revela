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
}
