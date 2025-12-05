using System.CommandLine;
using Spectara.Revela.Features.Generate.Services;
using Spectre.Console;

namespace Spectara.Revela.Features.Generate;

/// <summary>
/// Command to generate static site from content
/// </summary>
/// <remarks>
/// <para>
/// Orchestrates the site generation workflow:
/// </para>
/// <list type="number">
///   <item><description>Scan source/ directory for images and markdown</description></item>
///   <item><description>Process images (resize, convert, extract EXIF) - unless --skip-images</description></item>
///   <item><description>Parse markdown and frontmatter</description></item>
///   <item><description>Render templates with Scriban</description></item>
///   <item><description>Write output to output/ directory</description></item>
/// </list>
/// <para>
/// Fixed directory structure (convention over configuration):
/// </para>
/// <list type="bullet">
///   <item><description>source/ - Source images and content</description></item>
///   <item><description>output/ - Generated site</description></item>
///   <item><description>themes/ - Local theme overrides</description></item>
/// </list>
/// </remarks>
public sealed partial class GenerateCommand(
    ILogger<GenerateCommand> logger,
    SiteGenerator siteGenerator)
{
    private const string SourceDirectory = "source";
    private const string OutputDirectory = "output";
    private const string CacheDirectory = ".cache";

    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var command = new Command("generate", "Generate static site from content");

        // Options - minimal, like original expose.sh
        var skipImagesOption = new Option<bool>("--skip-images", "-s")
        {
            Description = "Skip image processing (HTML only, for theme development)"
        };

        var cleanOption = new Option<bool>("--clean", "-c")
        {
            Description = "Clean output directory before generation"
        };

        command.Options.Add(skipImagesOption);
        command.Options.Add(cleanOption);

        command.SetAction(async parseResult =>
        {
            var options = new GenerateOptions
            {
                SkipImages = parseResult.GetValue(skipImagesOption),
                Clean = parseResult.GetValue(cleanOption)
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
            if (!Directory.Exists(SourceDirectory))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Source directory not found: {SourceDirectory}");
                AnsiConsole.MarkupLine("[dim]Run 'revela source onedrive sync' to fetch content first.[/]");
                return;
            }

            // Clean output and cache directories if requested
            // Note: Cleans both output/ and .cache/ for a completely fresh build.
            if (options.Clean)
            {
                if (Directory.Exists(OutputDirectory))
                {
                    AnsiConsole.MarkupLine("[yellow]Cleaning output directory...[/]");
                    Directory.Delete(OutputDirectory, recursive: true);
                }

                if (Directory.Exists(CacheDirectory))
                {
                    AnsiConsole.MarkupLine("[yellow]Cleaning cache directory...[/]");
                    Directory.Delete(CacheDirectory, recursive: true);
                }
            }

            // Generate site
            AnsiConsole.MarkupLine("[blue]Generating site...[/]");
            AnsiConsole.MarkupLine($"[dim]Source:[/] {SourceDirectory}");
            AnsiConsole.MarkupLine($"[dim]Output:[/] {OutputDirectory}");
            if (options.SkipImages)
            {
                AnsiConsole.MarkupLine("[dim]Mode:[/] [yellow]HTML only (skipping images)[/]");
            }
            AnsiConsole.WriteLine();

            await siteGenerator.GenerateAsync(options, CancellationToken.None);

            // Success message
            var panel = new Panel(
                "[green]Site generated successfully![/]\n\n" +
                $"[bold]Output:[/] [cyan]{OutputDirectory}[/]\n\n" +
                "[dim]Next steps:[/]\n" +
                $"1. Open [cyan]{OutputDirectory}/index.html[/] in your browser\n" +
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
    /// <summary>
    /// Skip image processing (HTML only mode)
    /// </summary>
    /// <remarks>
    /// Useful for theme development - generates HTML without
    /// time-consuming image processing.
    /// </remarks>
    public bool SkipImages { get; init; }

    /// <summary>
    /// Clean output directory before generation
    /// </summary>
    public bool Clean { get; init; }
}
