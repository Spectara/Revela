using System.Globalization;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Plugins.Calendar;
using Spectara.Revela.Plugins.Calendar.Commands;
using Spectara.Revela.Plugins.Calendar.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

using Spectre.Console;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CalendarGenerateStepTests
{
    private const string PagePath = "availability [summer]";
    private const string SourceFileName = "bookings [rental].ics";
    private const string SentinelJson = /*lang=json,strict*/ """{"last_good":true}""";
    private const string EmptyCalendar = "BEGIN:VCALENDAR\nVERSION:2.0\nEND:VCALENDAR\n";
    private const string IncompleteCalendar = "BEGIN:VCALENDAR\nBEGIN:VEVENT\nDTSTART;VALUE=DATE:20260320\nEND:VEVENT\nEND:VCALENDAR\n";

    [TestMethod]
    [DataRow("<html>not a calendar</html>", "Expected BEGIN:VCALENDAR")]
    [DataRow(IncompleteCalendar, "DTEND")]
    [DataRow(" \r\n\t", "invalid")]
    [DataRow(null, "missing")]
    public async Task PipelineExecuteAsync_InvalidOrMissingInput_FailsAndPreservesLastGoodJson(string? content, string diagnostic)
    {
        using var project = TestProject.Create();
        var step = CreateStep(project, content);

        var result = await ((IPipelineStep)step).ExecuteAsync();

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains(PagePath, result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(SourceFileName, result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(diagnostic, result.ErrorMessage, StringComparison.Ordinal);
        Assert.AreEqual(SentinelJson, await File.ReadAllTextAsync(GetCalendarJsonPath(project)));
    }

    [TestMethod]
    [DataRow("<html>not a calendar</html>", "Expected BEGIN:VCALENDAR")]
    [DataRow(IncompleteCalendar, "DTEND")]
    [DataRow(" \r\n\t", "invalid")]
    [DataRow(null, "missing")]
    public async Task ExecuteAsync_InvalidOrMissingInput_ReturnsOneAndPreservesLastGoodJson(string? content, string diagnostic)
    {
        using var project = TestProject.Create();
        var step = CreateStep(project, content);

        var (exitCode, output) = await ExecuteCliAsync(step);

        Assert.AreEqual(1, exitCode);
        Assert.Contains(PagePath, output, StringComparison.Ordinal);
        Assert.Contains(SourceFileName, output, StringComparison.Ordinal);
        Assert.Contains(diagnostic, output, StringComparison.Ordinal);
        Assert.DoesNotContain("Calendar data generated!", output, StringComparison.Ordinal);
        Assert.AreEqual(SentinelJson, await File.ReadAllTextAsync(GetCalendarJsonPath(project)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_ValidEmptyCalendar_WritesFreeMonthsWithPageLocaleAndLabels(bool useCli)
    {
        using var project = TestProject.Create();
        var step = CreateStep(project, EmptyCalendar);

        await AssertSuccessfulExecutionAsync(step, useCli);

        var json = await File.ReadAllTextAsync(GetCalendarJsonPath(project));
        Assert.AreNotEqual(SentinelJson, json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var months = root.GetProperty("months");
        Assert.AreEqual(2, months.GetArrayLength());
        Assert.AreEqual(
            CultureInfo.GetCultureInfo("de-DE").DateTimeFormat.GetAbbreviatedDayName(DayOfWeek.Monday),
            root.GetProperty("day_names")[0].GetString());
        Assert.AreEqual("Available", root.GetProperty("labels").GetProperty("free").GetString());
        var days = months[1].GetProperty("weeks").EnumerateArray()
            .SelectMany(week => week.EnumerateArray())
            .Where(day => day.GetProperty("number").GetInt32() > 0)
            .ToList();
        Assert.IsNotEmpty(days);
        Assert.IsTrue(days.All(day => day.GetProperty("css").GetString() == "free"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_ValidBooking_WritesBookedDaysWithExclusiveEnd(bool useCli)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var bookingStart = new DateOnly(today.Year, today.Month, 10).AddMonths(1);
        var bookingEnd = bookingStart.AddDays(2);
        var content = "BEGIN:VCALENDAR\nVERSION:2.0\nBEGIN:VEVENT\n" +
            $"DTSTART;VALUE=DATE:{bookingStart.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}\n" +
            $"DTEND;VALUE=DATE:{bookingEnd.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}\n" +
            "END:VEVENT\nEND:VCALENDAR\n";
        using var project = TestProject.Create();
        var step = CreateStep(project, content);

        await AssertSuccessfulExecutionAsync(step, useCli);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(GetCalendarJsonPath(project)));
        var days = document.RootElement.GetProperty("months")[1].GetProperty("weeks").EnumerateArray()
            .SelectMany(week => week.EnumerateArray())
            .Where(day => day.GetProperty("number").GetInt32() > 0)
            .ToDictionary(day => day.GetProperty("number").GetInt32(), day => day.GetProperty("css").GetString());
        Assert.AreEqual("booked", days[10]);
        Assert.AreEqual("booked", days[11]);
        Assert.AreEqual("free", days[12]);
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public async Task Command_InPipelineSignal_ControlsNextStepsHint(bool inPipeline, bool expectHint)
    {
        using var project = TestProject.Create();
        var step = CreateStep(project, EmptyCalendar);
        var command = step.Create();
        // The host attaches this option to every step of an auto-generated "all" command.
        command.Options.Add(PipelineInvocation.InPipelineOption);
        string[] args = inPipeline ? [PipelineInvocation.InPipelineOption.Name] : [];

        var (exitCode, output) = await CaptureAsync(() => command.Parse(args).InvokeAsync());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Calendar data generated!", output, StringComparison.Ordinal);
        Assert.AreEqual(expectHint, output.Contains("Next steps:", StringComparison.Ordinal));
        Assert.AreEqual(expectHint, output.Contains("revela generate pages", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_PageNoLongerInManifest_DeletesStaleCalendarJson(bool useCli)
    {
        using var project = TestProject.Create();
        var step = CreateStep(project, EmptyCalendar);
        var stalePath = WriteCalendarJson(project, "removed [page]");

        await AssertSuccessfulExecutionAsync(step, useCli);

        Assert.IsFalse(File.Exists(stalePath));
        Assert.AreNotEqual(SentinelJson, await File.ReadAllTextAsync(GetCalendarJsonPath(project)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_NoCalendarPages_DeletesStaleCalendarJson(bool useCli)
    {
        using var project = TestProject.Create();
        var step = CreateStep(project, EmptyCalendar, withCalendarPage: false);

        if (useCli)
        {
            var (exitCode, output) = await ExecuteCliAsync(step);
            Assert.AreEqual(0, exitCode);
            Assert.Contains("No calendar pages", output, StringComparison.Ordinal);
        }
        else
        {
            var result = await ((IPipelineStep)step).ExecuteAsync();
            Assert.IsTrue(result.Success, result.ErrorMessage);
        }

        Assert.IsFalse(File.Exists(GetCalendarJsonPath(project)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_NoUsableManifest_ReportsMissingScanAndPreservesCalendarJson(bool useCli)
    {
        using var project = TestProject.Create();
        var step = CreateStep(project, EmptyCalendar, scanned: false);

        if (useCli)
        {
            var (exitCode, output) = await ExecuteCliAsync(step);
            Assert.AreEqual(1, exitCode);
            Assert.Contains("generate scan", output, StringComparison.Ordinal);
        }
        else
        {
            var result = await ((IPipelineStep)step).ExecuteAsync();
            Assert.IsFalse(result.Success);
            Assert.Contains("run scan first", result.ErrorMessage!, StringComparison.Ordinal);
        }

        Assert.AreEqual(SentinelJson, await File.ReadAllTextAsync(GetCalendarJsonPath(project)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_DependentInvalidationFails_FailsAndPreservesCalendarJson(bool useCli)
    {
        using var project = TestProject.Create();
        var lifecycle = Substitute.For<IArtifactLifecycle>();
        lifecycle.PrepareToReplaceAsync(CalendarArtifacts.Data, Arg.Any<CancellationToken>())
            .Returns(OperationResult.Fail("dependent cleanup failed"));
        var step = CreateStep(project, EmptyCalendar, artifactLifecycle: lifecycle);

        if (useCli)
        {
            var (exitCode, output) = await ExecuteCliAsync(step);
            Assert.AreEqual(1, exitCode);
            Assert.Contains("dependent cleanup failed", output, StringComparison.Ordinal);
        }
        else
        {
            var result = await ((IPipelineStep)step).ExecuteAsync();
            Assert.IsFalse(result.Success);
            Assert.Contains("dependent cleanup failed", result.ErrorMessage!, StringComparison.Ordinal);
        }

        Assert.AreEqual(SentinelJson, await File.ReadAllTextAsync(GetCalendarJsonPath(project)));
    }

    private static CalendarGenerateStep CreateStep(
        TestProject project,
        string? content,
        bool withCalendarPage = true,
        IArtifactLifecycle? artifactLifecycle = null,
        bool scanned = true)
    {
        var pageDirectory = Path.Combine(project.SourcePath, PagePath);
        Directory.CreateDirectory(pageDirectory);
        File.WriteAllText(Path.Combine(pageDirectory, "_index.revela"),
            $"+++\ncalendar.source = \"{SourceFileName}\"\ncalendar.months = 2\n" +
            "calendar.locale = \"de-DE\"\ncalendar.labels.free = \"Available\"\n+++\n");
        if (content is not null)
        {
            File.WriteAllText(Path.Combine(pageDirectory, SourceFileName), content);
        }

        var cacheDirectory = Path.Combine(project.RootPath, ProjectPaths.Cache);
        Directory.CreateDirectory(Path.Combine(cacheDirectory, PagePath));
        File.WriteAllText(Path.Combine(cacheDirectory, "manifest.json"), "{}");
        File.WriteAllText(GetCalendarJsonPath(project), SentinelJson);

        var manifestReader = Substitute.For<IManifestReader>();
        manifestReader.TryLoadAsync(Arg.Any<CancellationToken>()).Returns(scanned
            ? new ManifestSnapshot
            {
                Root = new ManifestEntry
                {
                    Text = "Availability",
                    Path = PagePath,
                    DataSources = withCalendarPage
                        ? new Dictionary<string, string> { ["calendar"] = "calendar.json" }
                        : []
                },
                Images = new Dictionary<string, ImageContent>()
            }
            : null);
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.SourcePath.Returns(project.SourcePath);

        if (artifactLifecycle is null)
        {
            artifactLifecycle = Substitute.For<IArtifactLifecycle>();
            artifactLifecycle.PrepareToReplaceAsync(Arg.Any<ArtifactId>(), Arg.Any<CancellationToken>())
                .Returns(OperationResult.Ok());
        }

        var projectEnvironment = Options.Create(new ProjectEnvironment { Path = project.RootPath });
        return new CalendarGenerateStep(
            NullLogger<CalendarGenerateStep>.Instance,
            manifestReader,
            projectEnvironment,
            Options.Create(new SiteCoreConfig { Language = "en" }),
            pathResolver,
            artifactLifecycle,
            new CalendarDataInvalidator(projectEnvironment));
    }

    private static string WriteCalendarJson(TestProject project, string pagePath)
    {
        var path = Path.Combine(project.RootPath, ProjectPaths.Cache, pagePath, "calendar.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, SentinelJson);
        return path;
    }

    private static string GetCalendarJsonPath(TestProject project) =>
        Path.Combine(project.RootPath, ProjectPaths.Cache, PagePath, "calendar.json");

    private static async Task AssertSuccessfulExecutionAsync(
        CalendarGenerateStep step, bool useCli, CancellationToken cancellationToken = default)
    {
        if (useCli)
        {
            var (exitCode, output) = await ExecuteCliAsync(step, cancellationToken);
            Assert.AreEqual(0, exitCode);
            Assert.Contains("Calendar data generated!", output, StringComparison.Ordinal);
        }
        else
        {
            var result = await ((IPipelineStep)step).ExecuteAsync(cancellationToken);
            Assert.IsTrue(result.Success, result.ErrorMessage);
        }
    }

    private static async Task<(int ExitCode, string Output)> ExecuteCliAsync(
        CalendarGenerateStep step, CancellationToken cancellationToken = default) =>
        await CaptureAsync(() => step.ExecuteAsync(cancellationToken: cancellationToken));

    private static async Task<(int ExitCode, string Output)> CaptureAsync(Func<Task<int>> execute)
    {
        var originalConsole = AnsiConsole.Console;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        AnsiConsole.Console.Profile.Width = 240;

        try
        {
            var exitCode = await execute();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
