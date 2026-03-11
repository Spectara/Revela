using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Plugins.Statistics.Commands;

/// <summary>
/// Command to generate statistics page from manifest EXIF data.
/// </summary>
/// <remarks>
/// <para>
/// Output: Creates statistics.json in .cache/{page.Path}/.
/// The actual rendering is done by the theme extension (Lumina.Statistics).
/// </para>
/// <para>
/// Implements <see cref="IGenerateStep"/> for pipeline integration.
/// </para>
/// </remarks>
internal sealed partial class StatsCommand(
    ILogger<StatsCommand> logger,
    IManifestRepository manifestRepository,
    IOptions<ProjectEnvironment> projectEnvironment,
    StatisticsAggregator aggregator) : IGenerateStep
{
    private const string ManifestFileName = "manifest.json";

    /// <inheritdoc />
    public string Name => "statistics";

    /// <inheritdoc />
    public string Description => "Generate statistics from EXIF data";

    /// <inheritdoc />
    public int Order => GenerateStepOrder.Statistics;

    /// <summary>
    /// Create the command
    /// </summary>
    public Command Create()
    {
        var command = new Command("statistics", "Generate statistics JSON from EXIF data");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var projectPath = projectEnvironment.Value.Path;

        // Check if manifest exists
        var manifestFile = Path.Combine(projectPath, ProjectPaths.Cache, ManifestFileName);
        if (!File.Exists(manifestFile))
        {
            ErrorPanels.ShowPrerequisiteError(
                "Site manifest",
                "generate scan",
                "The manifest contains all image metadata needed for statistics.");
            return 1;
        }
        // Load manifest
        LogLoadingManifest();
        await manifestRepository.LoadAsync(cancellationToken);

        if (manifestRepository.Images.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No images found in manifest.");
            return 0;
        }

        // Find all pages that need statistics (data = { statistics: "..." })
        var root = manifestRepository.Root ?? throw new InvalidOperationException("Manifest root is null after loading");
        var statsPages = FindStatisticsPages(root);

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
        foreach (var pagePath in statsPages)
        {
            // Aggregate statistics (TODO: filter by page metadata)
            var stats = aggregator.Aggregate();

            // Calculate output path in {ProjectPaths.Cache}/{pagePath}/
            // pagePath is already relative (e.g., "03 Pages\Statistics")
            var cacheDir = Path.Combine(projectPath, ProjectPaths.Cache, pagePath);
            var jsonPath = Path.Combine(cacheDir, "statistics.json");

            // Write JSON data file
            Directory.CreateDirectory(cacheDir);
            await JsonWriter.WriteAsync(jsonPath, stats, cancellationToken);
            LogGeneratedJsonFile(pagePath, stats.TotalImages);

            generatedCount++;
        }

        // Display summary
        var panel = new Panel(
            new Markup($"[green]Statistics generated![/]\n\n" +
                      $"[dim]Summary:[/]\n" +
                      $"  Pages:    {generatedCount}\n" +
                      $"  Images:   {manifestRepository.Images.Count}\n\n" +
                      "[dim]Next steps:[/]\n" +
                      "  • Run [cyan]revela generate pages[/] to render statistics pages\n" +
                      "  • Requires [cyan]Lumina.Statistics[/] extension for styling"))
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
    /// 1. Explicit data source: data = { statistics: "statistics.json" }
    /// 2. Statistics template: template = "statistics/..." (uses extension data defaults)
    /// </remarks>
    private static List<string> FindStatisticsPages(ManifestEntry root)
    {
        var results = new List<string>();
        FindRecursive(root, results);
        return results;

        static void FindRecursive(ManifestEntry node, List<string> results)
        {
            // Match explicit data source OR statistics template
            var hasStatisticsData = node.DataSources.ContainsKey("statistics");
            var hasStatisticsTemplate = node.Template?.StartsWith("statistics/", StringComparison.OrdinalIgnoreCase) == true;

            if (hasStatisticsData || hasStatisticsTemplate)
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating statistics for {Count} pages")]
    private partial void LogGeneratingStats(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated statistics JSON for {Path} ({Count} images)")]
    private partial void LogGeneratedJsonFile(string path, int count);
}

