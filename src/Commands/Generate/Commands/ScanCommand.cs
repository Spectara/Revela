using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to scan content and update manifest with galleries/navigation.
/// </summary>
/// <remarks>
/// <para>
/// Thin CLI wrapper that delegates to <see cref="IContentService.ScanAsync"/>.
/// Implements <see cref="IGenerateStep"/> for pipeline orchestration.
/// </para>
/// <para>
/// Usage: revela generate scan
/// </para>
/// </remarks>
public sealed partial class ScanCommand(
    ILogger<ScanCommand> logger,
    IContentService contentService,
    IOptionsMonitor<ProjectConfig> projectConfig) : IGenerateStep
{
    /// <inheritdoc />
    public string Name => "scan";

    /// <inheritdoc />
    public string Description => "Scan content and update manifest";

    /// <inheritdoc />
    public int Order => GenerateStepOrder.Scan;

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("scan", "Scan content and update manifest");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            _ = parseResult;
            return await ExecuteAsync(cancellationToken);
        });

        return command;
    }

    /// <summary>
    /// Executes the scan command.
    /// </summary>
    /// <remarks>
    /// Public to allow orchestration by <see cref="AllCommand"/>.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
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
                var projectName = projectConfig.CurrentValue.Name;
                if (string.IsNullOrEmpty(projectName))
                {
                    projectName = "Revela Site";
                }

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
                .WithHeader("[bold green]Success[/]")
                .WithSuccessStyle();

                AnsiConsole.Write(panel);
                return 0;
            }

            var errorPanel = new Panel(
                new Markup($"[red]{result.ErrorMessage}[/]"))
                .WithHeader("[bold red]Scan failed[/]")
                .WithErrorStyle();
            AnsiConsole.Write(errorPanel);
            LogScanFailed(logger);
            return 1;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Canceled[/]");
            LogScanFailed(logger);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan command failed")]
    private static partial void LogScanFailed(ILogger logger);
}

