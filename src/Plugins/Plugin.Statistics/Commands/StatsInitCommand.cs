using System.CommandLine;
using System.Text.Json;

using Spectre.Console;

using Spectara.Revela.Plugin.Statistics.Commands.Logging;

namespace Spectara.Revela.Plugin.Statistics.Commands;

/// <summary>
/// Command to initialize Statistics plugin configuration
/// </summary>
/// <remarks>
/// Creates config JSON and optional markdown file.
/// Command: revela init generate stats
/// </remarks>
public sealed class StatsInitCommand(ILogger<StatsInitCommand> logger)
{
    private const string PluginsFolderName = "plugins";
    private const string PluginPackageId = "Spectara.Revela.Plugin.Statistics";
    private const string DefaultConfigFileName = $"{PluginPackageId}.json";
    private const string DefaultOutputPath = "source/statistics";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Command Create()
    {
        var command = new Command("stats", "Initialize Statistics plugin configuration");

        var titleOption = new Option<string?>("--title", "-t")
        {
            Description = "Page title for the statistics page (default: Statistics)"
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = $"Output path for markdown file (default: {DefaultOutputPath})"
        };

        command.Options.Add(titleOption);
        command.Options.Add(outputOption);

        command.SetAction(parseResult =>
        {
            var title = parseResult.GetValue(titleOption);
            var outputPath = parseResult.GetValue(outputOption);
            Execute(title, outputPath);
            return 0;
        });

        return command;
    }

    private void Execute(string? title, string? outputPath)
    {
        try
        {
            var configPath = Path.Combine(PluginsFolderName, DefaultConfigFileName);

            // Ensure plugins folder exists
            Directory.CreateDirectory(PluginsFolderName);

            // Check if already initialized
            if (File.Exists(configPath))
            {
                if (!AnsiConsole.Confirm($"[yellow]{configPath} already exists. Overwrite?[/]"))
                {
                    AnsiConsole.MarkupLine("[dim]Configuration unchanged.[/]");
                    return;
                }
            }

            AnsiConsole.MarkupLine("[blue]Initializing Statistics plugin...[/]\n");

            // Resolve output path
            outputPath ??= DefaultOutputPath;
            title ??= "Statistics";

            // Create configuration
            var configObject = new Dictionary<string, object?>
            {
                [PluginPackageId] = new Dictionary<string, object?>
                {
                    ["OutputPath"] = outputPath,
                    ["MaxEntriesPerCategory"] = 15,
                    ["SortByFrequency"] = true,
                    ["MaxBarWidth"] = 100
                }
            };

            // Save config
            var json = JsonSerializer.Serialize(configObject, JsonOptions);
            File.WriteAllText(configPath, json);
            logger.ConfigCreated(configPath);

            // Create output directory
            Directory.CreateDirectory(outputPath);

            // Create initial markdown file
            var mdPath = Path.Combine(outputPath, "_index.md");
            if (!File.Exists(mdPath))
            {
                var frontmatter = $"""
                    ---
                    title: {title}
                    template: page
                    nav_order: 100
                    ---

                    Your photo library statistics will appear below after running `revela generate stats`.

                    """;
                File.WriteAllText(mdPath, frontmatter);
                logger.MarkdownCreated(mdPath);
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]Markdown file already exists: {mdPath}[/]");
            }

            // Success message
            var panel = new Panel(
                $"[green]Statistics plugin configured![/]\n\n" +
                $"[bold]Configuration:[/] [cyan]{configPath}[/]\n" +
                $"[bold]Output path:[/] [cyan]{outputPath}[/]\n" +
                $"[bold]Markdown:[/] [cyan]{mdPath}[/]\n\n" +
                $"[bold]Next steps:[/]\n" +
                $"1. Run [cyan]revela generate scan[/] to scan your images\n" +
                $"2. Run [cyan]revela generate stats[/] to generate statistics\n" +
                $"3. Run [cyan]revela generate[/] to build your site"
            )
            {
                Header = new PanelHeader("[bold green]Success[/]"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            logger.InitFailed(ex);
        }
    }
}
