using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectre.Console;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to process images from manifest.
/// </summary>
/// <remarks>
/// <para>
/// Thin CLI wrapper that delegates to <see cref="IImageService.ProcessAsync"/>.
/// </para>
/// <para>
/// Usage: revela generate images [--force]
/// </para>
/// </remarks>
public sealed partial class ImagesCommand(
    ILogger<ImagesCommand> logger,
    IImageService imageService,
    IManifestRepository manifestRepository,
    IConfiguration configuration)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("images", "Process images from manifest");

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Force rebuild all images (ignore cache)"
        };
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var force = parseResult.GetValue(forceOption);
            return await ExecuteAsync(force, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(bool force, CancellationToken cancellationToken)
    {
        try
        {
            // Early manifest check before showing progress bar
            await manifestRepository.LoadAsync(cancellationToken);

            if (manifestRepository.Root is null)
            {
                var warningPanel = new Panel(
                    "[yellow]No manifest found.[/]\n\n" +
                    "[dim]Solution:[/]\n" +
                    "Run [cyan]revela generate scan[/] first to scan your content."
                )
                {
                    Header = new PanelHeader("[bold yellow]Warning[/]"),
                    Border = BoxBorder.Rounded
                };

                AnsiConsole.Write(warningPanel);
                return 1;
            }

            var options = new ProcessImagesOptions { Force = force };

            if (force)
            {
                AnsiConsole.MarkupLine("[yellow]Force rebuild requested, processing all images...[/]");
            }

            var result = await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
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

                        var safeName = p.CurrentImage
                            .Replace("[", "[[", StringComparison.Ordinal)
                            .Replace("]", "]]", StringComparison.Ordinal);

                        task.Description = string.IsNullOrEmpty(safeName)
                            ? "[green]Processing images[/]"
                            : $"[green]Processing[/] {safeName}";
                    });

                    return await imageService.ProcessAsync(options, progress, cancellationToken);
                });

            if (result.Success)
            {
                var projectName = configuration["name"] ?? "Revela Site";

                var content = "[green]Image processing complete![/]\n\n";
                content += $"[dim]Project:[/]   [cyan]{projectName}[/]\n\n";
                content += "[dim]Statistics:[/]\n";
                content += $"  Processed: {result.ProcessedCount} images\n";

                if (result.SkippedCount > 0)
                {
                    content += $"  Cached:    {result.SkippedCount} images\n";
                }

                if (result.FilesCreated > 0)
                {
                    content += $"  Files:     {result.FilesCreated} created\n";
                    content += $"  Size:      {FormatSize(result.TotalSize)}\n";
                }

                content += $"  Duration:  {result.Duration.TotalSeconds:F2}s\n";
                content += "\n[dim]Next steps:[/]\n";
                content += "  • Run [cyan]revela generate pages[/] to render HTML\n";
                content += "  • Or run [cyan]revela generate[/] for full build";

                var successPanel = new Panel(new Markup(content))
                {
                    Header = new PanelHeader("[bold green]Success[/]"),
                    Border = BoxBorder.Rounded
                };
                AnsiConsole.Write(successPanel);
                return 0;
            }

            var errorPanel = new Panel(
                new Markup($"[red]{result.ErrorMessage}[/]"))
            {
                Header = new PanelHeader("[bold red]Image processing failed[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(errorPanel);
            LogImageProcessingFailed(logger);
            return 1;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Canceled[/]");
            LogImageProcessingFailed(logger);
            return 1;
        }
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
            < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:F1} KB", bytes / 1024.0),
            < 1024 * 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:F1} MB", bytes / (1024.0 * 1024.0)),
            _ => string.Format(CultureInfo.InvariantCulture, "{0:F2} GB", bytes / (1024.0 * 1024.0 * 1024.0))
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing command failed")]
    private static partial void LogImageProcessingFailed(ILogger logger);
}
