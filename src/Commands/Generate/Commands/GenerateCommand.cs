using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Spectara.Revela.Commands.Generate.Abstractions;
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
/// <para>
/// To clean output/cache before generating, use: revela clean --all
/// </para>
/// </remarks>
public sealed partial class GenerateCommand(
    ILogger<GenerateCommand> logger,
    IContentService contentService,
    IImageService imageService,
    IRenderService renderService,
    IConfiguration configuration,
    ScanCommand scanCommand,
    ImagesCommand imagesCommand,
    PagesCommand pagesCommand)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var command = new Command("generate", "Generate static site from content");

        command.SetAction(async parseResult =>
        {
            await ExecuteAsync();
            return 0;
        });

        // Add sub-commands
        command.Subcommands.Add(scanCommand.Create());
        command.Subcommands.Add(imagesCommand.Create());
        command.Subcommands.Add(pagesCommand.Create());

        return command;
    }

    private async Task ExecuteAsync()
    {
        var startTime = DateTime.UtcNow;

        AnsiConsole.MarkupLine("[blue]Generating site...[/]");
        AnsiConsole.MarkupLine("[dim]Source:[/] source");
        AnsiConsole.MarkupLine("[dim]Output:[/] output");
        AnsiConsole.WriteLine();

        // Phase 1: Scan
        var scanResult = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[yellow]🔍 Scanning content...[/]", async ctx =>
            {
                var progress = new Progress<ContentProgress>(
                    p => ctx.Status($"[yellow]🔍 {p.Status}[/] ({p.GalleriesFound} galleries, {p.ImagesFound} images)")
                );

                return await contentService.ScanAsync(progress, CancellationToken.None);
            });

        if (!scanResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]ERROR Scan failed:[/] {scanResult.ErrorMessage}");
            LogGenerationFailed(logger);
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Scanned: {scanResult.GalleryCount} galleries, {scanResult.ImageCount} images[/]");

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
                var task = ctx.AddTask("[green]Processing images[/]");
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
            AnsiConsole.MarkupLine($"[red]ERROR Image processing failed:[/] {imageResult.ErrorMessage}");
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
                var task = ctx.AddTask("[green]Rendering pages[/]");
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
            AnsiConsole.MarkupLine($"[red]ERROR Page generation failed:[/] {renderResult.ErrorMessage}");
            LogGenerationFailed(logger);
            return;
        }

        var duration = DateTime.UtcNow - startTime;
        var projectName = configuration["name"] ?? "Revela Site";

        // Success message with detailed stats
        var content = "[green]Site generated successfully![/]\n\n";
        content += $"[dim]Project:[/]   [cyan]{projectName}[/]\n";
        content += "[dim]Output:[/]    [cyan]output/[/]\n\n";
        content += "[dim]Statistics:[/]\n";
        content += $"  Galleries:  {scanResult.GalleryCount}\n";
        content += $"  Images:     {imageResult.ProcessedCount} processed";
        if (imageResult.SkippedCount > 0)
        {
            content += $", {imageResult.SkippedCount} cached";
        }

        content += "\n";
        content += $"  Pages:      {renderResult.PageCount}\n\n";
        content += "[dim]Timing:[/]\n";
        content += $"  Scan:       {scanResult.Duration.TotalSeconds:F2}s\n";
        content += $"  Images:     {imageResult.Duration.TotalSeconds:F2}s\n";
        content += $"  Render:     {renderResult.Duration.TotalSeconds:F2}s\n";
        content += $"  [bold]Total:[/]      {duration.TotalSeconds:F2}s\n\n";
        content += "[dim]Next steps:[/]\n";
        content += "  • Open [cyan]output/index.html[/] in your browser\n";
        content += "  • Deploy with [cyan]revela deploy[/] (coming soon)";

        var panel = new Panel(new Markup(content))
        {
            Header = new PanelHeader("[bold green]Success[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Site generation failed")]
    private static partial void LogGenerationFailed(ILogger logger);
}
