using System.CommandLine;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectre.Console;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to generate static site from content
/// </summary>
/// <remarks>
/// <para>
/// Orchestrates the site generation workflow:
/// </para>
/// <list type="number">
///   <item><description>Scan source/ directory for images and markdown</description></item>
///   <item><description>Process images (resize, convert, extract EXIF)</description></item>
///   <item><description>Render templates with Scriban</description></item>
///   <item><description>Write output to output/ directory</description></item>
/// </list>
/// <para>
/// Sub-commands for granular control:
/// </para>
/// <list type="bullet">
///   <item><description>revela generate scan - Scan content only</description></item>
///   <item><description>revela generate images - Process images only</description></item>
///   <item><description>revela generate pages - Render pages only</description></item>
/// </list>
/// </remarks>
public sealed partial class GenerateCommand(
    ILogger<GenerateCommand> logger,
    IContentService contentService,
    IImageService imageService,
    IRenderService renderService,
    ScanCommand scanCommand,
    ImagesCommand imagesCommand,
    PagesCommand pagesCommand)
{
    /// <summary>Output directory for generated site</summary>
    private const string OutputDirectory = "output";

    /// <summary>Cache directory name</summary>
    private const string CacheDirectory = ".cache";

    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var command = new Command("generate", "Generate static site from content");

        // Options
        var cleanOption = new Option<bool>("--clean", "-c")
        {
            Description = "Clean output directory before generation"
        };

        command.Options.Add(cleanOption);

        command.SetAction(async parseResult =>
        {
            var options = new GenerateOptions
            {
                Clean = parseResult.GetValue(cleanOption)
            };

            await ExecuteAsync(options);
            return 0;
        });

        // Add sub-commands
        command.Subcommands.Add(scanCommand.Create());
        command.Subcommands.Add(imagesCommand.Create());
        command.Subcommands.Add(pagesCommand.Create());

        return command;
    }

    private async Task ExecuteAsync(GenerateOptions options)
    {
        var startTime = DateTime.UtcNow;

        AnsiConsole.MarkupLine("[blue]Generating site...[/]");
        AnsiConsole.MarkupLine("[dim]Source:[/] source");
        AnsiConsole.MarkupLine("[dim]Output:[/] output");
        AnsiConsole.WriteLine();

        // Clean if requested
        if (options.Clean)
        {
            AnsiConsole.MarkupLine("[yellow]Cleaning output directory...[/]");

            if (Directory.Exists(OutputDirectory))
            {
                Directory.Delete(OutputDirectory, recursive: true);
            }

            if (Directory.Exists(CacheDirectory))
            {
                Directory.Delete(CacheDirectory, recursive: true);
            }
        }

        // Phase 1: Scan
        var scanResult = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[yellow]🔍 Scanning content...[/]", async ctx =>
            {
                var progress = new Progress<ContentProgress>(p =>
                {
                    ctx.Status($"[yellow]🔍 {p.Status}[/] ({p.GalleriesFound} galleries, {p.ImagesFound} images)");
                });

                return await contentService.ScanAsync(progress, CancellationToken.None);
            });

        if (!scanResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]✗ Scan failed:[/] {scanResult.ErrorMessage}");
            LogGenerationFailed(logger);
            return;
        }

        AnsiConsole.MarkupLine($"[dim]✓ Scanned: {scanResult.GalleryCount} galleries, {scanResult.ImageCount} images[/]");

        // Phase 2: Images
        var imageResult = await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]🖼️ Processing images[/]");
                task.IsIndeterminate = true;

                var progress = new Progress<ImageProgress>(p =>
                {
                    if (task.IsIndeterminate && p.Total > 0)
                    {
                        task.IsIndeterminate = false;
                        task.MaxValue = p.Total;
                    }

                    task.Value = p.Processed;
                });

                return await imageService.ProcessAsync(new ProcessImagesOptions(), progress, CancellationToken.None);
            });

        if (!imageResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]✗ Image processing failed:[/] {imageResult.ErrorMessage}");
            LogGenerationFailed(logger);
            return;
        }

        // Phase 3: Pages
        var renderResult = await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]📄 Rendering pages[/]");
                task.IsIndeterminate = true;

                var progress = new Progress<RenderProgress>(p =>
                {
                    if (task.IsIndeterminate && p.Total > 0)
                    {
                        task.IsIndeterminate = false;
                        task.MaxValue = p.Total;
                    }

                    task.Value = p.Rendered;
                });

                return await renderService.RenderAsync(progress, CancellationToken.None);
            });

        if (!renderResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]✗ Page generation failed:[/] {renderResult.ErrorMessage}");
            LogGenerationFailed(logger);
            return;
        }

        var duration = DateTime.UtcNow - startTime;

        // Success message
        var panel = new Panel(
            "[green]Site generated successfully![/]\n\n" +
            $"[bold]Output:[/] [cyan]output[/]\n" +
            $"[bold]Duration:[/] {duration.TotalSeconds:F2}s\n\n" +
            $"[dim]Stats:[/]\n" +
            $"  Galleries: {scanResult.GalleryCount}\n" +
            $"  Images: {imageResult.ProcessedCount} processed, {imageResult.SkippedCount} cached\n" +
            $"  Pages: {renderResult.PageCount}\n\n" +
            "[dim]Next steps:[/]\n" +
            "1. Open [cyan]output/index.html[/] in your browser\n" +
            "2. Deploy with [cyan]revela deploy[/] (coming soon)"
        )
        {
            Header = new PanelHeader("[bold green]Success[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Site generation failed")]
    private static partial void LogGenerationFailed(ILogger logger);
}
