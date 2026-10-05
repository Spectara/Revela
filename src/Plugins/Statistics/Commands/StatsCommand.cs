using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Plugins.Statistics.Commands;

/// <summary>
/// Command to generate statistics page from manifest EXIF data.
/// </summary>
/// <remarks>
/// Output: Creates the page's data file (<c>data.statistics</c>, default <c>statistics.json</c>)
/// in .revela/statistics/{page.Path}/.
/// The actual rendering is done by the theme extension (Lumina.Statistics).
/// </remarks>
internal sealed partial class StatsCommand(
    ILogger<StatsCommand> logger,
    IManifestReader manifestReader,
    IOptions<ProjectEnvironment> projectEnvironment,
    StatisticsAggregator aggregator,
    IArtifactLifecycle artifactLifecycle,
    StatisticsDataInvalidator statisticsDataInvalidator) : IPipelineStep
{
    private const string DataFileExtension = ".json";

    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Generate;

    string IPipelineStep.Name => "statistics";


    async ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        var projectPath = projectEnvironment.Value.Path;
        var manifest = await manifestReader.TryLoadAsync(cancellationToken);
        if (manifest is null)
        {
            return OperationResult.Fail("Manifest not found — run scan first");
        }

        var statsPages = FindStatisticsPages(manifest.Root);
        var invalidPage = FindInvalidDataFileName(statsPages);
        if (invalidPage is not null)
        {
            return OperationResult.Fail(InvalidDataFileNameMessage(invalidPage));
        }

        var invalidationResult = await artifactLifecycle.PrepareToReplaceAsync(
            StatisticsArtifacts.Data,
            cancellationToken);
        if (!invalidationResult.Success)
        {
            return invalidationResult;
        }

        var cleanupResult = await statisticsDataInvalidator.InvalidateAsync(cancellationToken);
        if (!cleanupResult.Success)
        {
            return OperationResult.Fail(
                cleanupResult.ErrorMessage ?? "Statistics artifact cleanup failed");
        }

        if (manifest.Images.Count == 0)
        {
            return OperationResult.Ok();
        }

        if (statsPages.Count == 0)
        {
            return OperationResult.Ok();
        }

        foreach (var page in statsPages)
        {
            var stats = aggregator.Aggregate(manifest);
            var cacheDir = Path.Combine(StatisticsDataInvalidator.GetDataDirectory(projectPath), page.Path);
            var jsonPath = Path.Combine(cacheDir, page.DataFileName);
            Directory.CreateDirectory(cacheDir);
            await JsonWriter.WriteAsync(jsonPath, stats, cancellationToken);
        }

        return OperationResult.Ok();
    }

    // ── CLI command ──

    /// <summary>
    /// Create the command
    /// </summary>
    public Command Create()
    {
        var command = new Command("statistics", "Generate statistics JSON from EXIF data");

        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(parseResult.IsInPipeline(), cancellationToken));

        return command;
    }

    /// <summary>
    /// Executes the statistics command.
    /// </summary>
    /// <param name="inPipeline">Whether the step runs inside a pipeline (suppresses standalone hints).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ExecuteAsync(bool inPipeline = false, CancellationToken cancellationToken = default)
    {
        var projectPath = projectEnvironment.Value.Path;

        LogLoadingManifest();
        var manifest = await manifestReader.TryLoadAsync(cancellationToken);
        if (manifest is null)
        {
            ErrorPanels.ShowPrerequisiteError(
                "Site manifest",
                "generate scan",
                "The manifest contains all image metadata needed for statistics.");
            return 1;
        }

        // Find all pages that need statistics (data = { statistics: "..." })
        var statsPages = FindStatisticsPages(manifest.Root);
        var invalidPage = FindInvalidDataFileName(statsPages);
        if (invalidPage is not null)
        {
            ErrorPanels.ShowError(
                "Invalid Statistics Data File",
                $"[yellow]{Markup.Escape(InvalidDataFileNameMessage(invalidPage))}[/]");
            return 1;
        }

        var invalidationResult = await artifactLifecycle.PrepareToReplaceAsync(
            StatisticsArtifacts.Data,
            cancellationToken);
        if (!invalidationResult.Success)
        {
            ErrorPanels.ShowError(
                "Statistics Invalidation Failed",
                $"[yellow]{Markup.Escape(invalidationResult.ErrorMessage ?? "Unknown error")}[/]");
            return 1;
        }

        var cleanupResult = await statisticsDataInvalidator.InvalidateAsync(cancellationToken);
        if (!cleanupResult.Success)
        {
            ErrorPanels.ShowError(
                "Statistics Cleanup Failed",
                $"[yellow]{Markup.Escape(cleanupResult.ErrorMessage ?? "Unknown error")}[/]");
            return 1;
        }

        if (manifest.Images.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No images found in manifest.");
            return 0;
        }

        if (statsPages.Count == 0)
        {
            ErrorPanels.ShowWarning(
                "No Statistics Pages",
                "[yellow]No statistics pages found in manifest.[/]\n\n" +
                "Create a page with [cyan]data = { statistics: \"statistics.json\" }[/] in frontmatter.");
            return 0;
        }

        LogGeneratingStats(statsPages.Count);

        var generatedCount = 0;
        foreach (var page in statsPages)
        {
            // Aggregate statistics (TODO: filter by page metadata)
            var stats = aggregator.Aggregate(manifest);

            // Calculate output path in .revela/statistics/{pagePath}/
            // page.Path is already relative (e.g., "03 Pages\Statistics")
            var cacheDir = Path.Combine(StatisticsDataInvalidator.GetDataDirectory(projectPath), page.Path);
            var jsonPath = Path.Combine(cacheDir, page.DataFileName);

            // Write JSON data file
            Directory.CreateDirectory(cacheDir);
            await JsonWriter.WriteAsync(jsonPath, stats, cancellationToken);
            LogGeneratedJsonFile(page.Path, stats.TotalImages);

            generatedCount++;
        }

        // Display summary
        var content =
            $"[green]Statistics generated![/]\n\n" +
            $"[dim]Summary:[/]\n" +
            $"  Pages:    {generatedCount}\n" +
            $"  Images:   {manifest.Images.Count}";

        if (!inPipeline)
        {
            content +=
                "\n\n[dim]Next steps:[/]\n" +
                "  • Run [cyan]revela generate pages[/] to render statistics pages\n" +
                "  • Requires [cyan]Lumina.Statistics[/] extension for styling";
        }

        var panel = new Panel(new Markup(content))
            .WithHeader("[bold green]Success[/]")
            .WithSuccessStyle();
        AnsiConsole.Write(panel);

        return 0;
    }

    /// <summary>
    /// Recursively find all pages with statistics data source or statistics template
    /// </summary>
    /// <remarks>
    /// Matches pages with:
    /// 1. Explicit data source: data = { statistics: "statistics.json" } — the page's file name is used
    /// 2. Statistics template: template = "statistics/..." (uses the extension's default, statistics.json)
    /// </remarks>
    private static List<StatisticsPage> FindStatisticsPages(ManifestEntry root)
    {
        var results = new List<StatisticsPage>();
        FindRecursive(root, results);
        return results;

        static void FindRecursive(ManifestEntry node, List<StatisticsPage> results)
        {
            // Match explicit data source OR statistics template
            var hasStatisticsData = node.DataSources.TryGetValue("statistics", out var dataFileName);
            var hasStatisticsTemplate = node.Template?.StartsWith("statistics/", StringComparison.OrdinalIgnoreCase) == true;

            if (hasStatisticsData || hasStatisticsTemplate)
            {
                results.Add(new StatisticsPage(node.Path, dataFileName ?? StatisticsDataInvalidator.DefaultFileName));
            }

            foreach (var child in node.Children)
            {
                FindRecursive(child, results);
            }
        }
    }

    private static StatisticsPage? FindInvalidDataFileName(IEnumerable<StatisticsPage> pages) =>
        pages.FirstOrDefault(page => !IsDataFileName(page.DataFileName));

    private static string InvalidDataFileNameMessage(StatisticsPage page) =>
        $"Statistics page '{page.Path}' sets data.statistics to '{page.DataFileName}'. " +
        $"Use a file name ending in .json without folders, e.g. \"{StatisticsDataInvalidator.DefaultFileName}\".";

    /// <summary>
    /// Returns whether <paramref name="fileName"/> is a bare <c>*.json</c> file name
    /// (no folder, no drive, no <c>..</c>), so the data stays in the page's folder below
    /// <c>.revela/statistics</c>.
    /// </summary>
    private static bool IsDataFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal)
        && fileName.Length > DataFileExtension.Length
        && fileName.EndsWith(DataFileExtension, StringComparison.Ordinal)
        && fileName.IndexOfAny(['/', '\\', ':']) < 0
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>A statistics page and the data file name its template reads.</summary>
    private sealed record StatisticsPage(string Path, string DataFileName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading manifest...")]
    private partial void LogLoadingManifest();

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating statistics for {Count} pages")]
    private partial void LogGeneratingStats(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated statistics JSON for {Path} ({Count} images)")]
    private partial void LogGeneratedJsonFile(string path, int count);
}

