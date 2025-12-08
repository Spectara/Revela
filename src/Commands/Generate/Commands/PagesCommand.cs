using System.CommandLine;
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
    IRenderService renderService)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("pages", "Generate HTML pages from manifest");

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
            AnsiConsole.MarkupLine($"[green]✓ Pages generated![/] {result.PageCount} pages");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ Page generation failed:[/] {result.ErrorMessage}");
            LogPagesGenerationFailed(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Page generation command failed")]
    private static partial void LogPagesGenerationFailed(ILogger logger);
}
