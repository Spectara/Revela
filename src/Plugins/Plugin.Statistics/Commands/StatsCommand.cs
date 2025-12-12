using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectara.Revela.Commands.Generate.Abstractions;
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
/// Output: Creates _index.md with front-matter and partial reference.
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

        // Create aggregator with resolved dependencies
        logger.Aggregating(manifestRepository.Images.Count);
        var aggregator = new StatisticsAggregator(manifestRepository, config, logger);

        // Aggregate statistics
        var stats = aggregator.Aggregate();

        // Determine output path
        var settings = config.CurrentValue;
        var outputPath = outputOverride ?? settings.OutputPath;
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), outputPath);
        var jsonPath = Path.Combine(outputDir, "statistics.json");
        var mdPath = Path.Combine(outputDir, "_index.md");

        // Write JSON data file (for template consumption)
        logger.WritingJson();
        await JsonWriter.WriteAsync(jsonPath, stats, cancellationToken).ConfigureAwait(false);

        // Write _index.md with front-matter and partial reference
        logger.WritingMarkdown();
        await WriteIndexMarkdownAsync(mdPath, cancellationToken).ConfigureAwait(false);

        // Display summary
        var panel = new Panel(
            new Markup($"[green]Statistics generated![/]\n\n" +
                      $"[dim]Summary:[/]\n" +
                      $"  Images:   {stats.TotalImages} ({stats.ImagesWithExif} with EXIF)\n" +
                      $"  Cameras:  {stats.CameraModels.Count}\n" +
                      $"  Lenses:   {stats.LensModels.Count}\n" +
                      $"  Galleries: {stats.TotalGalleries}\n\n" +
                      $"[dim]Output:[/]   [cyan]{Path.GetFullPath(outputDir)}[/]\n\n" +
                      "[dim]Next steps:[/]\n" +
                      "  • Run [cyan]revela generate pages[/] to include in your site\n" +
                      "  • Requires [cyan]Theme.Lumina.Statistics[/] extension for styling"))
        {
            Header = new PanelHeader("[bold green]Success[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);

        return 0;
    }

    /// <summary>
    /// Write _index.md with front-matter and partial reference
    /// </summary>
    private static async Task WriteIndexMarkdownAsync(string filePath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        const string content = """
            ---
            title: "Photo Statistics"
            description: "EXIF statistics from your photo library"
            template: statistics/overview
            data:
              statistics: statistics.json
            ---
            """;

        await File.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);
    }
}
