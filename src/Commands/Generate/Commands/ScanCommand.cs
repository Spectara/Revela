using System.CommandLine;
using Microsoft.Extensions.Configuration;
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
    IContentService contentService,
    IConfiguration configuration)
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
                var progress = new Progress<ContentProgress>(p => ctx.Status($"[yellow]{p.Status}[/] ({p.GalleriesFound} galleries, {p.ImagesFound} images)"));

                return await contentService.ScanAsync(progress, cancellationToken);
            });

        if (result.Success)
        {
            var projectName = configuration["name"] ?? "Revela Site";

            var panel = new Panel(
                $"[green]Content scan complete![/]\n\n" +
                $"[dim]Project:[/]    [cyan]{projectName}[/]\n\n" +
                $"[dim]Statistics:[/]\n" +
                $"  Galleries:  {result.GalleryCount}\n" +
                $"  Images:     {result.ImageCount}\n" +
                $"  Navigation: {result.NavigationItemCount}\n" +
                $"  Duration:   {result.Duration.TotalSeconds:F2}s\n\n" +
                $"[dim]Next steps:[/]\n" +
                $"  • Run [cyan]revela generate images[/] to process images\n" +
                $"  • Run [cyan]revela generate pages[/] to render HTML\n" +
                $"  • Or run [cyan]revela generate[/] for full build"
            )
            {
                Header = new PanelHeader("[bold green]Success[/]"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
        }
        else
        {
            var panel = new Panel(
                new Markup($"[red]{result.ErrorMessage}[/]"))
            {
                Header = new PanelHeader("[bold red]Scan failed[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(panel);
            LogScanFailed(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan command failed")]
    private static partial void LogScanFailed(ILogger logger);
}
