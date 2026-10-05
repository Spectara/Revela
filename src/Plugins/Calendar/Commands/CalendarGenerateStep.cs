using System.CommandLine;
using System.Globalization;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Calendar.Models;
using Spectara.Revela.Plugins.Calendar.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Calendar.Commands;

/// <summary>
/// Generate step that reads local .ics files and produces the calendar data file
/// (<c>data.calendar</c>, default <c>calendar.json</c>) for each calendar page.
/// </summary>
/// <remarks>
/// All calendar pages are built in memory first. Only when every page succeeded are the
/// previous data files removed and the new ones written, so a broken page keeps the last
/// good data and pages that no longer exist leave no stale data behind.
/// </remarks>
internal sealed partial class CalendarGenerateStep(
    ILogger<CalendarGenerateStep> logger,
    IManifestReader manifestReader,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptions<SiteCoreConfig> siteCoreConfig,
    IPathResolver pathResolver,
    IArtifactLifecycle artifactLifecycle,
    CalendarDataInvalidator calendarDataInvalidator,
    TimeProvider timeProvider) : IPipelineStep
{

    private const string IndexFileName = "_index.revela";

    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Generate;

    string IPipelineStep.Name => "calendar";

    async ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        var outcome = await GenerateAsync(cancellationToken);
        return outcome.Status switch
        {
            GenerationStatus.ManifestMissing => OperationResult.Fail("Manifest not found — run scan first"),
            GenerationStatus.Failed => OperationResult.Fail(outcome.ErrorMessage ?? "Calendar generation failed"),
            GenerationStatus.Generated or GenerationStatus.NoPages => OperationResult.Ok(),
            _ => throw new InvalidOperationException($"Unexpected calendar generation status '{outcome.Status}'."),
        };
    }

    // ── CLI command ──

    /// <summary>
    /// Creates the CLI command for standalone execution.
    /// </summary>
    public Command Create()
    {
        var command = new Command("calendar", "Generate availability calendar from iCal data");

        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(parseResult.IsInPipeline(), cancellationToken));

        return command;
    }

    /// <summary>
    /// Executes the calendar command.
    /// </summary>
    /// <param name="inPipeline">Whether the step runs inside a pipeline (suppresses standalone hints).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ExecuteAsync(bool inPipeline = false, CancellationToken cancellationToken = default)
    {
        var outcome = await GenerateAsync(cancellationToken);

        switch (outcome.Status)
        {
            case GenerationStatus.ManifestMissing:
                ErrorPanels.ShowPrerequisiteError(
                    "Site manifest",
                    "generate scan",
                    "The manifest contains page metadata needed for calendar generation.");
                return 1;

            case GenerationStatus.Failed:
                ErrorPanels.ShowError(
                    outcome.ErrorTitle ?? "Calendar Generation Failed",
                    Markup.Escape(outcome.ErrorMessage ?? "Unknown error"));
                return 1;

            case GenerationStatus.NoPages:
                ErrorPanels.ShowWarning(
                    "No Calendar Pages",
                    "[yellow]No calendar pages found in manifest.[/]\n\n" +
                    "Create a page with [cyan]data.calendar = \"calendar.json\"[/] in frontmatter.");
                return 0;

            case GenerationStatus.Generated:
                break;

            default:
                throw new InvalidOperationException($"Unexpected calendar generation status '{outcome.Status}'.");
        }

        var content =
            $"[green]Calendar data generated![/]\n\n" +
            $"[dim]Summary:[/]\n" +
            $"  Pages:  {outcome.GeneratedCount}";

        if (!inPipeline)
        {
            content +=
                "\n\n[dim]Next steps:[/]\n" +
                "  • Run [cyan]revela generate pages[/] to render calendar pages";
        }

        var panel = new Panel(new Markup(content))
            .WithHeader("[bold green]Success[/]")
            .WithSuccessStyle();
        AnsiConsole.Write(panel);

        return 0;
    }

    // ── Shared generation core ──

    private async Task<GenerationOutcome> GenerateAsync(CancellationToken cancellationToken)
    {
        var projectPath = projectEnvironment.Value.Path;
        var sourcePath = pathResolver.SourcePath;

        LogLoadingManifest();
        var manifest = await manifestReader.TryLoadAsync(cancellationToken);
        if (manifest is null)
        {
            return GenerationOutcome.ManifestMissing;
        }

        var calendarPages = FindCalendarPages(manifest.Root);
        if (calendarPages.Count > 0)
        {
            LogGeneratingCalendars(calendarPages.Count);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var calendars = new List<(CalendarPage Page, CalendarData Data)>();

        foreach (var page in calendarPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pagePath = page.Path;

            if (!CalendarInputPaths.IsDataFileName(page.DataFileName))
            {
                return GenerationOutcome.Failure(
                    "Invalid Calendar Data File",
                    $"Calendar page '{pagePath}' sets data.calendar to '{page.DataFileName}'. " +
                    $"Use a file name ending in .json without folders, e.g. \"{CalendarDataInvalidator.DefaultFileName}\".");
            }

            var pageDirectory = Path.Combine(sourcePath, pagePath);
            var indexPath = Path.Combine(pageDirectory, IndexFileName);
            if (!File.Exists(indexPath))
            {
                LogIndexFileNotFound(pagePath);
                continue;
            }

            var indexContent = await File.ReadAllTextAsync(indexPath, cancellationToken);
            var pageConfig = FrontmatterReader.Read(indexContent);
            if (pageConfig is null)
            {
                LogNoCalendarConfig(pagePath);
                continue;
            }

            var icsPath = CalendarInputPaths.ResolveSource(sourcePath, pageDirectory, pageConfig.Source);
            if (icsPath is null)
            {
                return GenerationOutcome.Failure(
                    "iCal File Outside Source",
                    $"Calendar page '{pagePath}' references iCalendar file '{pageConfig.Source}' outside the source folder. " +
                    "Place the .ics file inside the source folder, e.g. next to the page.");
            }

            if (!File.Exists(icsPath))
            {
                return GenerationOutcome.Failure(
                    "iCal File Not Found",
                    $"Calendar page '{pagePath}' references missing iCalendar file '{pageConfig.Source}'.");
            }

            var icsContent = await File.ReadAllTextAsync(icsPath, cancellationToken);
            if (!TryParseCalendar(icsContent, out var bookings, out var errorMessage))
            {
                return GenerationOutcome.Failure(
                    "Invalid iCal File",
                    $"Calendar page '{pagePath}' has invalid iCalendar file '{pageConfig.Source}': {errorMessage}");
            }

            LogParsedBookings(pagePath, bookings.Count);

            var labels = pageConfig.Labels ?? new CalendarLabels();
            var culture = ResolveCulture(pageConfig, siteCoreConfig.Value);
            if (culture is null)
            {
                LogInvalidLocale(pageConfig.Locale ?? siteCoreConfig.Value.Language, pagePath);
            }

            calendars.Add((page, CalendarBuilder.Build(bookings, pageConfig.Months, today, pageConfig.Mode, labels, culture)));
        }

        var invalidation = await artifactLifecycle.PrepareToReplaceAsync(CalendarArtifacts.Data, cancellationToken);
        if (!invalidation.Success)
        {
            return GenerationOutcome.Failure("Calendar Invalidation Failed", invalidation.ErrorMessage ?? "Unknown error");
        }

        var cleanup = await calendarDataInvalidator.InvalidateAsync(cancellationToken);
        if (!cleanup.Success)
        {
            return GenerationOutcome.Failure("Calendar Cleanup Failed", cleanup.ErrorMessage ?? "Unknown error");
        }

        foreach (var (page, calendarData) in calendars)
        {
            var cacheDir = Path.Combine(CalendarDataInvalidator.GetDataDirectory(projectPath), page.Path);
            Directory.CreateDirectory(cacheDir);
            var json = JsonSerializer.Serialize(calendarData, CalendarJsonContext.Default.CalendarData);
            await File.WriteAllTextAsync(Path.Combine(cacheDir, page.DataFileName), json, cancellationToken);
            LogGeneratedJson(page.Path, calendarData.Months.Count);
        }

        return calendarPages.Count == 0
            ? GenerationOutcome.NoPages
            : GenerationOutcome.Generated(calendars.Count);
    }

    private enum GenerationStatus
    {
        Generated,
        NoPages,
        ManifestMissing,
        Failed,
    }

    private sealed record GenerationOutcome(
        GenerationStatus Status,
        int GeneratedCount = 0,
        string? ErrorTitle = null,
        string? ErrorMessage = null)
    {
        public static GenerationOutcome ManifestMissing { get; } = new(GenerationStatus.ManifestMissing);

        public static GenerationOutcome NoPages { get; } = new(GenerationStatus.NoPages);

        public static GenerationOutcome Generated(int count) => new(GenerationStatus.Generated, count);

        public static GenerationOutcome Failure(string title, string message) =>
            new(GenerationStatus.Failed, ErrorTitle: title, ErrorMessage: message);
    }
    private static bool TryParseCalendar(string icsContent, out IReadOnlyList<BookingRange> bookings, out string errorMessage)
    {
        try
        {
            bookings = ICalParser.Parse(icsContent);
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            bookings = [];
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Resolves the <see cref="CultureInfo"/> for a calendar page: the per-page
    /// <c>calendar.locale</c> when set, otherwise the site language from
    /// <c>site.json</c> (which defaults to <c>"en"</c>). Returns <see langword="null"/>
    /// when the resolved locale is not a valid culture, so the caller can fall back
    /// to the invariant culture.
    /// </summary>
    /// <param name="pageConfig">The per-page calendar configuration.</param>
    /// <param name="siteCoreConfig">The validated site identity core.</param>
    internal static CultureInfo? ResolveCulture(
        CalendarPageConfig pageConfig,
        SiteCoreConfig siteCoreConfig)
    {
        var locale = pageConfig.Locale ?? siteCoreConfig.Language;

        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>A calendar page and the data file name its template reads.</summary>
    private sealed record CalendarPage(string Path, string DataFileName);

    /// <summary>
    /// Finds the calendar pages and the data file each one reads: the page's
    /// <c>data.calendar</c>, or the extension's default <c>calendar.json</c> for pages
    /// that only set a <c>calendar/*</c> template.
    /// </summary>
    private static List<CalendarPage> FindCalendarPages(ManifestEntry root)
    {
        var results = new List<CalendarPage>();
        FindRecursive(root, results);
        return results;

        static void FindRecursive(ManifestEntry node, List<CalendarPage> results)
        {
            var hasCalendarData = node.DataSources.TryGetValue("calendar", out var dataFileName);
            var hasCalendarTemplate = node.Template?.StartsWith("calendar/", StringComparison.OrdinalIgnoreCase) == true;

            if (hasCalendarData || hasCalendarTemplate)
            {
                results.Add(new CalendarPage(node.Path, dataFileName ?? CalendarDataInvalidator.DefaultFileName));
            }

            foreach (var child in node.Children)
            {
                FindRecursive(child, results);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading manifest...")]
    private partial void LogLoadingManifest();

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating calendars for {Count} pages")]
    private partial void LogGeneratingCalendars(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Index file not found for calendar page: {Path}")]
    private partial void LogIndexFileNotFound(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No calendar configuration in frontmatter for page: {Path}")]
    private partial void LogNoCalendarConfig(string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Parsed {Count} bookings from iCal for page: {Path}")]
    private partial void LogParsedBookings(string path, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid locale '{Locale}' for page '{Path}', using InvariantCulture")]
    private partial void LogInvalidLocale(string locale, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated calendar JSON for {Path} ({MonthCount} months)")]
    private partial void LogGeneratedJson(string path, int monthCount);
}
