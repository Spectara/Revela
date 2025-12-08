using System.CommandLine;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectre.Console;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to scan content and update manifest with galleries/navigation.
/// </summary>
/// <remarks>
/// <para>
/// Thin CLI wrapper that delegates to <see cref="IContentService.ScanAsync"/>.
/// </para>
/// <para>
/// Usage: revela generate scan
/// </para>
/// </remarks>
public sealed partial class ScanCommand(
    ILogger<ScanCommand> logger,
    IContentService contentService)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("scan", "Scan content and update manifest");

        command.SetAction(async parseResult =>
        {
            _ = parseResult; // Unused but required by SetAction
            await ExecuteAsync(CancellationToken.None);
            return 0;
        });

        return command;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[yellow]Scanning content...[/]", async ctx =>
            {
                var progress = new Progress<ContentProgress>(p =>
                {
                    ctx.Status($"[yellow]{p.Status}[/] ({p.GalleriesFound} galleries, {p.ImagesFound} images)");
                });

                return await contentService.ScanAsync(progress, cancellationToken);
            });

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]✓ Scan complete![/] {result.GalleryCount} galleries, {result.ImageCount} images");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ Scan failed:[/] {result.ErrorMessage}");
            LogScanFailed(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan command failed")]
    private static partial void LogScanFailed(ILogger logger);
}
