using System.Globalization;
using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Plugins.Source.Calendar.Commands;
using Spectara.Revela.Plugins.Source.Calendar.Configuration;
using Spectara.Revela.Plugins.Source.Calendar.Services;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

using Spectre.Console;

namespace Spectara.Revela.Tests.Plugins.Source.Calendar;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CalendarFetchCommandTests
{
    [TestMethod]
    [DataRow("../outside.ics")]
    [DataRow("..\\outside.ics")]
    [DataRow("/outside.ics")]
    [DataRow("C:\\outside.ics")]
    [DataRow("bookings.ics:stream")]
    [DataRow(".")]
    [DataRow("")]
    public async Task ExecuteAsync_UnsafeOutput_PerformsNoRequests(string destination)
    {
        using var project = TestProject.CreateMinimal();
        using var handler = new FeedHandler();
        var settings = new SourceCalendarConfig();
        settings.Feeds.Add("first", new FeedConfig { Url = "https://example.com/good", Output = "first.ics" });
        settings.Feeds.Add("unsafe", new FeedConfig { Url = "https://example.com/secret-token", Output = destination });

        var (exitCode, output) = await InvokeAsync(project, settings, handler);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(0, handler.RequestCount);
        Assert.IsFalse(File.Exists(Path.Combine(project.SourcePath, "first.ics")));
        Assert.DoesNotContain("secret-token", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_OneFeedFails_ReturnsFailureAndKeepsExistingFile()
    {
        using var project = TestProject.CreateMinimal();
        Directory.CreateDirectory(project.SourcePath);
        var existing = Path.Combine(project.SourcePath, "failed.ics");
        await File.WriteAllTextAsync(existing, "previous data");
        using var handler = new FeedHandler();
        var settings = new SourceCalendarConfig();
        settings.Feeds.Add("good", new FeedConfig { Url = "https://example.com/good", Output = "good.ics" });
        settings.Feeds.Add("failed", new FeedConfig { Url = "https://example.com/fail", Output = "failed.ics" });

        var (exitCode, output) = await InvokeAsync(project, settings, handler);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(2, handler.RequestCount);
        Assert.IsTrue(File.Exists(Path.Combine(project.SourcePath, "good.ics")));
        Assert.AreEqual("previous data", await File.ReadAllTextAsync(existing));
        Assert.DoesNotContain("secret-token", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("same.ics", "nested/../same.ics")]
    [DataRow("a.ics", "a.ics/b.ics")]
    [DataRow("a.ics/b.ics", "a.ics")]
    public async Task ExecuteAsync_ConflictingOutputs_FailsBeforeDownloading(string first, string second)
    {
        using var project = TestProject.CreateMinimal();
        using var handler = new FeedHandler();
        var settings = new SourceCalendarConfig();
        settings.Feeds.Add("first", new FeedConfig { Url = "https://example.com/good", Output = first });
        settings.Feeds.Add("second", new FeedConfig { Url = "https://example.com/other", Output = second });

        var (exitCode, _) = await InvokeAsync(project, settings, handler);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(0, handler.RequestCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_UnusableDestination_FailsBeforeAnyDownload(bool fileParent)
    {
        using var project = TestProject.CreateMinimal();
        Directory.CreateDirectory(project.SourcePath);
        var existing = Path.Combine(project.SourcePath, "blocked");
        if (fileParent)
        {
            await File.WriteAllTextAsync(existing, "not a directory");
        }
        else
        {
            Directory.CreateDirectory(existing);
        }

        using var handler = new FeedHandler();
        var settings = new SourceCalendarConfig();
        settings.Feeds.Add("first", new FeedConfig { Url = "https://example.com/good", Output = "first.ics" });
        settings.Feeds.Add("blocked", new FeedConfig { Url = "https://example.com/good", Output = fileParent ? "blocked/bookings.ics" : "blocked" });

        var (exitCode, _) = await InvokeAsync(project, settings, handler);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(0, handler.RequestCount);
        Assert.IsFalse(File.Exists(Path.Combine(project.SourcePath, "first.ics")));
    }

    [TestMethod]
    public async Task ExecuteAsync_LinkedDirectory_PerformsNoRequestsOrOutsideWrites()
    {
        using var project = TestProject.CreateMinimal();
        using var outside = TestProject.CreateMinimal();
        Directory.CreateDirectory(project.SourcePath);
        var link = Path.Combine(project.SourcePath, "linked");
        DirectoryLinkTestHelper.Create(link, outside.RootPath);
        try
        {
            using var handler = new FeedHandler();
            var settings = new SourceCalendarConfig();
            settings.Feeds.Add("linked", new FeedConfig { Url = "https://example.com/good", Output = "linked/bookings.ics" });

            var (exitCode, _) = await InvokeAsync(project, settings, handler);

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(0, handler.RequestCount);
            Assert.IsFalse(File.Exists(Path.Combine(outside.RootPath, "bookings.ics")));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(link);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ValidNestedOutput_ReturnsSuccess()
    {
        using var project = TestProject.CreateMinimal();
        using var handler = new FeedHandler();
        var settings = new SourceCalendarConfig();
        settings.Feeds.Add("good", new FeedConfig { Url = "https://example.com/good", Output = "availability/bookings.ics" });

        var (exitCode, _) = await InvokeAsync(project, settings, handler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("BEGIN:VCALENDAR\nEND:VCALENDAR", await File.ReadAllTextAsync(Path.Combine(project.SourcePath, "availability", "bookings.ics")));
    }

    private static async Task<(int ExitCode, string Output)> InvokeAsync(TestProject project, SourceCalendarConfig settings, FeedHandler handler)
    {
        var services = new ServiceCollection();
        services.AddOptions<SourceCalendarConfig>().Configure(options =>
        {
            foreach (var (name, feed) in settings.Feeds)
            {
                options.Feeds.Add(name, feed);
            }
        });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<SourceCalendarConfig>>();
        var paths = Substitute.For<IPathResolver>();
        paths.SourcePath.Returns(project.SourcePath);
        using var client = new HttpClient(handler, disposeHandler: false);
        var fetcher = new ICalFetcher(client, NullLogger<ICalFetcher>.Instance);
        var command = new CalendarFetchCommand(NullLogger<CalendarFetchCommand>.Instance, fetcher, options, paths).Create();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var previousConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 240;
        AnsiConsole.Console = console;
        try
        {
            return (await command.Parse([]).InvokeAsync(), writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
        }
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.RequestUri!.AbsolutePath == "/fail")
            {
                throw new HttpRequestException("Upstream rejected secret-token");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("BEGIN:VCALENDAR\nEND:VCALENDAR") });
        }
    }
}
