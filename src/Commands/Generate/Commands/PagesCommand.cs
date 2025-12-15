using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectre.Console;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to generate HTML pages from manifest.
/// </summary>
/// <remarks>
/// <para>
/// Thin CLI wrapper that delegates to <see cref="IRenderService.RenderAsync"/>.
/// </para>
/// <para>
/// Usage: revela generate pages
/// </para>
/// </remarks>
public sealed partial class PagesCommand(
    ILogger<PagesCommand> logger,
    IRenderService renderService,
    IManifestRepository manifestRepository,
    IConfiguration configuration)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("pages", "Generate HTML pages from manifest");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            _ = parseResult;
            return await ExecuteAsync(cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
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

            var result = await AnsiConsole.Progress()
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

                        var safeName = p.CurrentPage
                            .Replace("[", "[[", StringComparison.Ordinal)
                            .Replace("]", "]]", StringComparison.Ordinal);

                        task.Description = $"[green]Rendering[/] {safeName}";
                    });

                    return await renderService.RenderAsync(progress, cancellationToken);
                });

            if (result.Success)
            {
                var projectName = configuration["name"] ?? "Revela Site";

                var successPanel = new Panel(
                    new Markup($"[green]Page rendering complete![/]\n\n" +
                              $"[dim]Project:[/]  [cyan]{projectName}[/]\n\n" +
                              $"[dim]Statistics:[/]\n" +
                              $"  Pages:    {result.PageCount}\n" +
                              $"  Duration: {result.Duration.TotalSeconds:F2}s\n\n" +
                              "[dim]Next steps:[/]\n" +
                              "  • Open [cyan]output/index.html[/] in your browser\n" +
                              "  • Run [cyan]revela generate[/] for full generation"))
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
                Header = new PanelHeader("[bold red]Page generation failed[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(errorPanel);
            LogPagesGenerationFailed(logger);
            return 1;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Canceled[/]");
            LogPagesGenerationFailed(logger);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Page generation command failed")]
    private static partial void LogPagesGenerationFailed(ILogger logger);
}
