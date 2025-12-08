using System.CommandLine;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectre.Console;

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
    IImageService imageService)
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

        command.SetAction(async parseResult =>
        {
            var force = parseResult.GetValue(forceOption);
            await ExecuteAsync(force, CancellationToken.None);
            return 0;
        });

        return command;
    }

    private async Task ExecuteAsync(bool force, CancellationToken cancellationToken)
    {
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
            if (result.SkippedCount > 0)
            {
                AnsiConsole.MarkupLine($"[dim]Skipped {result.SkippedCount} unchanged images[/]");
            }

            if (result.ProcessedCount == 0 && result.SkippedCount > 0)
            {
                AnsiConsole.MarkupLine("[green]OK All images up to date![/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]OK Processed {result.ProcessedCount} images[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]ERROR Image processing failed:[/] {result.ErrorMessage}");
            LogImageProcessingFailed(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing command failed")]
    private static partial void LogImageProcessingFailed(ILogger logger);
}
