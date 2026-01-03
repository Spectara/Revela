using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Statistics.Commands.Logging;
using Spectara.Revela.Plugin.Statistics.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Statistics.Commands;

/// <summary>
/// Command to generate statistics page from manifest EXIF data.
/// </summary>
/// <remarks>
/// <para>
/// Output: Creates statistics.json in .cache/{page.Path}/.
/// The actual rendering is done by the theme extension (Theme.Lumina.Statistics).
/// </para>
/// <para>
/// Implements <see cref="IGenerateStep"/> for pipeline integration.
/// </para>
/// </remarks>
public sealed class StatsCommand(
    ILogger<StatsCommand> logger,
    IManifestRepository manifestRepository,
    IOptions<ProjectEnvironment> projectEnvironment,
    StatisticsAggregator aggregator) : IGenerateStep
{
    private const string ManifestPath = ".cache/manifest.json";

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

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var projectPath = projectEnvironment.Value.Path;

        // Check if manifest exists
        var manifestFile = Path.Combine(projectPath, ManifestPath);
        if (!File.Exists(manifestFile))
        {
            ErrorPanels.ShowPrerequisiteError(
                "Site manifest",
                "generate scan",
                "The manifest contains all image metadata needed for statistics.");
            return 1;
        }
        // Load manifest
        logger.LoadingManifest();
        await manifestRepository.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (manifestRepository.Images.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] No images found in manifest.");
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

        logger.GeneratingStats(statsPages.Count);

        var generatedCount = 0;
        foreach (var page in statsPages)
        {
            // Aggregate statistics (TODO: filter by page metadata)
            var stats = aggregator.Aggregate();

            // Calculate output path in .cache/{page.Path}/
            // page.Path is already relative (e.g., "03 Pages\Statistics")
            var cacheDir = Path.Combine(projectPath, ".cache", page.Path);
            var jsonPath = Path.Combine(cacheDir, "statistics.json");

            // Write JSON data file
            Directory.CreateDirectory(cacheDir);
            await JsonWriter.WriteAsync(jsonPath, stats, cancellationToken).ConfigureAwait(false);
            logger.GeneratedJsonFile(page.Path, stats.TotalImages);

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
                      "  • Requires [cyan]Theme.Lumina.Statistics[/] extension for styling"))
            .WithHeader("[bold green]Success[/]")
            .WithSuccessStyle();
        AnsiConsole.Write(panel);

        return 0;
    }

    /// <summary>
    /// Recursively find all pages with statistics data source
    /// </summary>
    private static List<(string Path, string Slug)> FindStatisticsPages(ManifestEntry root)
    {
        var results = new List<(string Path, string Slug)>();
        FindRecursive(root, results);
        return results;

        static void FindRecursive(ManifestEntry node, List<(string Path, string Slug)> results)
        {
            if (node.DataSources.ContainsKey("statistics"))
            {
                results.Add((node.Path, node.Slug ?? string.Empty));
            }

            foreach (var child in node.Children)
            {
                FindRecursive(child, results);
            }
        }
    }
}

