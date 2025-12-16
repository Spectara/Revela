using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Manifest;
using Spectara.Revela.Plugin.Statistics.Commands.Logging;
using Spectara.Revela.Plugin.Statistics.Configuration;
using Spectara.Revela.Plugin.Statistics.Services;

namespace Spectara.Revela.Plugin.Statistics.Commands;

/// <summary>
/// Command to generate statistics page from manifest EXIF data
/// </summary>
/// <remarks>
/// <para>
/// Uses IServiceProvider for lazy resolution of IManifestRepository
/// to avoid DI validation issues during startup.
/// IManifestRepository is registered by Commands.AddGenerateFeature()
/// and resolved only when the command executes.
/// </para>
/// <para>
/// Output: Creates _index.revela with frontmatter and template reference.
/// The actual rendering is done by the theme extension (Theme.Lumina.Statistics).
/// </para>
/// </remarks>
public sealed class StatsCommand(
    ILogger<StatsCommand> logger,
    IServiceProvider serviceProvider,
    IOptionsMonitor<StatisticsPluginConfig> config)
{
    private const string ManifestPath = ".cache/manifest.json";

    /// <summary>
    /// Create the command
    /// </summary>
    public Command Create()
    {
        var command = new Command("stats", "Generate statistics page from EXIF data");

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output directory for statistics page (default: from config or source/statistics)"
        };
        command.Options.Add(outputOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var outputOverride = parseResult.GetValue(outputOption);
            return await ExecuteAsync(outputOverride, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string? outputOverride, CancellationToken cancellationToken)
    {
        _ = outputOverride; // Reserved for future per-page output override

        // Check if manifest exists
        var manifestFile = Path.Combine(Directory.GetCurrentDirectory(), ManifestPath);
        if (!File.Exists(manifestFile))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No manifest found.");
            AnsiConsole.MarkupLine("[yellow]Run 'revela generate scan' first to scan your images.[/]");
            return 1;
        }

        // Lazy-resolve IManifestRepository (depends on Commands infrastructure)
        var manifestRepository = serviceProvider.GetRequiredService<IManifestRepository>();

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
            AnsiConsole.MarkupLine("[yellow]No statistics pages found in manifest.[/]");
            AnsiConsole.MarkupLine("[dim]Create a page with [cyan]data = { statistics: \"statistics.json\" }[/] in frontmatter.[/]");
            return 0;
        }

        logger.GeneratingStats(statsPages.Count);

        // Create aggregator with resolved dependencies
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        var generatedCount = 0;
        foreach (var page in statsPages)
        {
            // Aggregate statistics (TODO: filter by page metadata)
            var stats = aggregator.Aggregate();

            // Calculate output path in .cache/{page.Path}/
            // page.Path is already relative (e.g., "03 Pages\Statistics")
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".cache", page.Path);
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
        {
            Header = new PanelHeader("[bold green]Success[/]"),
            Border = BoxBorder.Rounded
        };
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
