using System.CommandLine;
using Spectara.Revela.Features.Generate.Services;
using Spectre.Console;

namespace Spectara.Revela.Features.Generate;

/// <summary>
/// Command to generate static site from content
/// </summary>
/// <remarks>
/// Orchestrates the site generation workflow:
/// 1. Scan source/ directory for images and markdown
/// 2. Process images (resize, convert, extract EXIF)
/// 3. Parse markdown and frontmatter
/// 4. Render templates with Scriban
/// 5. Write output to output/ directory
/// </remarks>
public sealed partial class GenerateCommand(
    ILogger<GenerateCommand> logger,
    SiteGenerator siteGenerator)
{
    public Command Create()
    {
        var command = new Command("generate", "Generate static site from content");

        // Options
        var sourceOption = new Option<string?>("--source", "-s")
        {
            Description = "Source directory containing images and content (default: ./source)"
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output directory for generated site (default: ./output)"
        };

        var cleanOption = new Option<bool>("--clean", "-c")
        {
            Description = "Clean output directory before generation"
        };

        var watchOption = new Option<bool>("--watch", "-w")
        {
            Description = "Watch for changes and regenerate automatically (future feature)"
        };

        command.Options.Add(sourceOption);
        command.Options.Add(outputOption);
        command.Options.Add(cleanOption);
        command.Options.Add(watchOption);

        command.SetAction(async parseResult =>
        {
            var options = new GenerateOptions
            {
                SourceDirectory = parseResult.GetValue(sourceOption) ?? "source",
                OutputDirectory = parseResult.GetValue(outputOption) ?? "output",
                Clean = parseResult.GetValue(cleanOption),
                Watch = parseResult.GetValue(watchOption)
            };

            await ExecuteAsync(options);
            return 0;
        });

        return command;
    }

    private async Task ExecuteAsync(GenerateOptions options)
    {
        try
        {
            // Validate source directory exists
            if (!Directory.Exists(options.SourceDirectory))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Source directory not found: {options.SourceDirectory}");
                AnsiConsole.MarkupLine("[dim]Run 'revela source onedrive download' to fetch content first.[/]");
                return;
            }

            // Watch mode not yet implemented
            if (options.Watch)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Watch mode not yet implemented (coming in v1.1)");
                AnsiConsole.MarkupLine("[dim]Generating once...[/]\n");
            }

            // Clean output directory if requested
            if (options.Clean && Directory.Exists(options.OutputDirectory))
            {
                AnsiConsole.MarkupLine("[yellow]Cleaning output directory...[/]");
                Directory.Delete(options.OutputDirectory, recursive: true);
            }

            // Generate site
            AnsiConsole.MarkupLine("[blue]Generating site...[/]");
            AnsiConsole.MarkupLine($"[dim]Source:[/] {options.SourceDirectory}");
            AnsiConsole.MarkupLine($"[dim]Output:[/] {options.OutputDirectory}");
            AnsiConsole.WriteLine();

            await siteGenerator.GenerateAsync(options, CancellationToken.None);

            // Success message
            var panel = new Panel(
                "[green]Site generated successfully![/]\n\n" +
                $"[bold]Output:[/] [cyan]{options.OutputDirectory}[/]\n\n" +
                "[dim]Next steps:[/]\n" +
                $"1. Open [cyan]{options.OutputDirectory}/index.html[/] in your browser\n" +
                "2. Deploy with [cyan]revela deploy[/] (coming soon)"
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
            LogGenerationFailed(logger, ex);
        }
    }

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Error, Message = "Site generation failed")]
    static partial void LogGenerationFailed(ILogger logger, Exception exception);
}

/// <summary>
/// Options for site generation
/// </summary>
public sealed class GenerateOptions
{
    public required string SourceDirectory { get; init; }
    public required string OutputDirectory { get; init; }
    public bool Clean { get; init; }
    public bool Watch { get; init; }
}
