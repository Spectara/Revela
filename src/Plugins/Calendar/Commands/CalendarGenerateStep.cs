using System.CommandLine;
using System.Globalization;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Calendar.Models;
using Spectara.Revela.Plugins.Calendar.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Calendar.Commands;

/// <summary>
/// Generate step that reads local .ics files and produces calendar.json for each calendar page.
/// </summary>
internal sealed partial class CalendarGenerateStep(
    ILogger<CalendarGenerateStep> logger,
    IManifestRepository manifestRepository,
    IOptions<ProjectEnvironment> projectEnvironment,
    IPathResolver pathResolver) : IPipelineStep
{
    private const string ManifestFileName = "manifest.json";
    private const string CalendarJsonFileName = "calendar.json";
    private const string IndexFileName = "_index.revela";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Generate;

    string IPipelineStep.Name => "calendar";


    async ValueTask<PipelineStepResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        var projectPath = projectEnvironment.Value.Path;
        var sourcePath = pathResolver.SourcePath;

        var manifestFile = Path.Combine(projectPath, ProjectPaths.Cache, ManifestFileName);
        if (!File.Exists(manifestFile))
        {
            return PipelineStepResult.Fail("Manifest not found — run scan first");
        }

        await manifestRepository.LoadAsync(cancellationToken);

        var root = manifestRepository.Root ?? throw new InvalidOperationException("Manifest root is null after loading");
        var calendarPages = FindCalendarPages(root);

        if (calendarPages.Count == 0)
        {
            return PipelineStepResult.Ok();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var pagePath in calendarPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var indexPath = Path.Combine(sourcePath, pagePath, IndexFileName);
            if (!File.Exists(indexPath))
            {
                continue;
            }

            var indexContent = await File.ReadAllTextAsync(indexPath, cancellationToken);
            var pageConfig = FrontmatterReader.Read(indexContent);
            if (pageConfig is null)
            {
                continue;
            }

            var icsPath = Path.Combine(sourcePath, pagePath, pageConfig.Source);
            if (!File.Exists(icsPath))
            {
                continue;
            }

            var icsContent = await File.ReadAllTextAsync(icsPath, cancellationToken);
            var bookings = ICalParser.Parse(icsContent);

            var labels = pageConfig.Labels ?? new CalendarLabels();
            CultureInfo? culture = null;
            if (pageConfig.Locale is not null)
            {
                try
                { culture = CultureInfo.GetCultureInfo(pageConfig.Locale); }
                catch (CultureNotFoundException) { /* use invariant */ }
            }

            var calendarData = CalendarBuilder.Build(bookings, pageConfig.Months, today, pageConfig.Mode, labels, culture);

            var cacheDir = Path.Combine(projectPath, ProjectPaths.Cache, pagePath);
            var jsonPath = Path.Combine(cacheDir, CalendarJsonFileName);
            Directory.CreateDirectory(cacheDir);
            var json = JsonSerializer.Serialize(calendarData, JsonOptions);
            await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
        }

        return PipelineStepResult.Ok();
    }

    // ── CLI command ──

    /// <summary>
    /// Creates the CLI command for standalone execution.
    /// </summary>
    public Command Create()
    {
        var command = new Command("calendar", "Generate availability calendar from iCal data");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var projectPath = projectEnvironment.Value.Path;
        var sourcePath = pathResolver.SourcePath;

        // Check if manifest exists
        var manifestFile = Path.Combine(projectPath, ProjectPaths.Cache, ManifestFileName);
        if (!File.Exists(manifestFile))
        {
            ErrorPanels.ShowPrerequisiteError(
                "Site manifest",
                "generate scan",
                "The manifest contains page metadata needed for calendar generation.");
            return 1;
        }

        // Load manifest
        LogLoadingManifest();
        await manifestRepository.LoadAsync(cancellationToken);

        // Find pages with calendar data source
        var root = manifestRepository.Root ?? throw new InvalidOperationException("Manifest root is null after loading");
        var calendarPages = FindCalendarPages(root);

        if (calendarPages.Count == 0)
        {
            ErrorPanels.ShowWarning(
                "No Calendar Pages",
                "[yellow]No calendar pages found in manifest.[/]\n\n" +
                "Create a page with [cyan]data.calendar = \"calendar.json\"[/] in frontmatter.");
            return 0;
        }

        LogGeneratingCalendars(calendarPages.Count);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var generatedCount = 0;

        foreach (var pagePath in calendarPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Read per-page config from _index.revela frontmatter
            var indexPath = Path.Combine(sourcePath, pagePath, IndexFileName);
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

            // Read local .ics file
            var icsPath = Path.Combine(sourcePath, pagePath, pageConfig.Source);
            if (!File.Exists(icsPath))
            {
                ErrorPanels.ShowWarning(
                    "iCal File Not Found",
                    $"[yellow]Expected:[/] [cyan]{icsPath}[/]\n\n" +
                    "Run [cyan]revela source calendar fetch[/] to download the iCal feed,\n" +
                    "or place the .ics file manually.");
                continue;
            }

            var icsContent = await File.ReadAllTextAsync(icsPath, cancellationToken);
            var bookings = ICalParser.Parse(icsContent);
            LogParsedBookings(pagePath, bookings.Count);

            // Resolve labels (page overrides > defaults)
            var labels = pageConfig.Labels ?? new CalendarLabels();

            // Resolve locale
            CultureInfo? culture = null;
            if (pageConfig.Locale is not null)
            {
                try
                {
                    culture = CultureInfo.GetCultureInfo(pageConfig.Locale);
                }
                catch (CultureNotFoundException)
                {
                    LogInvalidLocale(pageConfig.Locale, pagePath);
                }
            }

            // Build calendar data
            var calendarData = CalendarBuilder.Build(bookings, pageConfig.Months, today, pageConfig.Mode, labels, culture);

            // Write to .cache/{pagePath}/calendar.json
            var cacheDir = Path.Combine(projectPath, ProjectPaths.Cache, pagePath);
            var jsonPath = Path.Combine(cacheDir, CalendarJsonFileName);

            Directory.CreateDirectory(cacheDir);
            var json = JsonSerializer.Serialize(calendarData, JsonOptions);
            await File.WriteAllTextAsync(jsonPath, json, cancellationToken);

            LogGeneratedJson(pagePath, calendarData.Months.Count);
            generatedCount++;
        }

        // Display summary
        var panel = new Panel(
            new Markup($"[green]Calendar data generated![/]\n\n" +
                       $"[dim]Summary:[/]\n" +
                       $"  Pages:  {generatedCount}\n\n" +
                       "[dim]Next steps:[/]\n" +
                       "  • Run [cyan]revela generate pages[/] to render calendar pages"))
            .WithHeader("[bold green]Success[/]")
            .WithSuccessStyle();
        AnsiConsole.Write(panel);

        return 0;
    }

    private static List<string> FindCalendarPages(ManifestEntry root)
    {
        var results = new List<string>();
        FindRecursive(root, results);
        return results;

        static void FindRecursive(ManifestEntry node, List<string> results)
        {
            var hasCalendarData = node.DataSources.ContainsKey("calendar");
            var hasCalendarTemplate = node.Template?.StartsWith("calendar/", StringComparison.OrdinalIgnoreCase) == true;

            if (hasCalendarData || hasCalendarTemplate)
            {
                results.Add(node.Path);
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
