using System.Collections.Concurrent;
using System.Net;

using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Plugins.Source.Calendar;
using Spectara.Revela.Plugins.Source.Calendar.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Source.Calendar;

[TestClass]
[TestCategory("Integration")]
public sealed class SourceCalendarHttpTests
{
    [TestMethod]
    public void ConfigureServices_DisablesAutomaticRedirectsAndCookies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        new SourceCalendarPlugin().ConfigureServices(services);
        using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(ICalFetcher));
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        var primary = handler as HttpClientHandler;
        Assert.IsNotNull(primary);
        Assert.IsFalse(primary.AllowAutoRedirect);
        Assert.IsFalse(primary.UseCookies);
    }

    [TestMethod]
    public async Task FetchAsync_FactoryLoggingEnabled_DoesNotRecordFeedCredentials()
    {
        using var project = TestProject.CreateMinimal();
        using var capture = new CaptureProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        new SourceCalendarPlugin().ConfigureServices(services);
        services.AddHttpClient<ICalFetcher>().ConfigurePrimaryHttpMessageHandler(() => new ResponseHandler());
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<ICalFetcher>().FetchAsync(
            "https://example.com/path-credential/calendar.ics?token=query-credential", Path.Combine(project.SourcePath, "bookings.ics"));

        var logs = string.Join('\n', capture.Entries);
        Assert.Contains("example.com", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("path-credential", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("query-credential", logs, StringComparison.Ordinal);
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("BEGIN:VCALENDAR\nEND:VCALENDAR") });
    }

    private sealed class CaptureProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Entries);

        public void Dispose() { }
    }

    private sealed class CaptureLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            entries.Enqueue(state.ToString() ?? string.Empty);
            return null;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(formatter(state, exception));
            entries.Enqueue(exception?.ToString() ?? string.Empty);
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (var value in values)
                {
                    entries.Enqueue(value.Value?.ToString() ?? string.Empty);
                }
            }
        }
    }
}
