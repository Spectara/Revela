using System.CommandLine;
using System.Diagnostics;

using Spectara.Revela.Sdk.Abstractions;

using Spectre.Console;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to execute the full generation pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Executes all registered <see cref="IGeneratePipelineStep"/> implementations
/// in order. Core steps (scan, pages, images) are built-in; plugins can add
/// additional steps (e.g., statistics).
/// </para>
/// <para>
/// Order 0 ensures this appears first in the menu.
/// </para>
/// </remarks>
public sealed partial class AllCommand(
    ILogger<AllCommand> logger,
    IEnumerable<IGeneratePipelineStep> pipelineSteps)
{
    /// <summary>
    /// Order for this command (first in menu).
    /// </summary>
    public const int Order = 0;

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("all", "Execute full pipeline (scan → statistics → pages → images)");

        command.SetAction(async (_, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    /// <summary>
    /// Executes all pipeline steps in order.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var steps = pipelineSteps.OrderBy(s => s.Order).ToList();

        if (steps.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No pipeline steps registered.[/]");
            return 0;
        }

        var pipelineNames = string.Join(" → ", steps.Select(s => s.Name));
        AnsiConsole.MarkupLine($"[blue]Executing pipeline:[/] {pipelineNames}");
        AnsiConsole.WriteLine();

        LogPipelineStart(logger, steps.Count);
        var stopwatch = Stopwatch.StartNew();

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AnsiConsole.MarkupLine($"[cyan]▶ {step.Name}[/] - {step.Description}");

            var result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"[yellow]{step.Description}...[/]", async ctx =>
                {
                    var progress = new Progress<PipelineProgress>(p =>
                    {
                        var status = p.Total > 0
                            ? $"[yellow]{step.Name}[/] ({p.Current}/{p.Total}) {p.Status}"
                            : $"[yellow]{step.Name}[/] {p.Status}";
                        ctx.Status(status);
                    });

                    return await step.ExecuteAsync(progress, cancellationToken);
                });

            if (!result.Success)
            {
                AnsiConsole.MarkupLine($"[red]✗ {step.Name} failed:[/] {result.Message}");
                LogStepFailed(logger, step.Name, result.Message ?? "Unknown error");
                return 1;
            }

            var info = result.Message is not null ? $" ({result.Message})" : "";
            AnsiConsole.MarkupLine($"[green]✓ {step.Name}[/]{info}");
            AnsiConsole.WriteLine();
        }

        stopwatch.Stop();
        AnsiConsole.MarkupLine($"[green]✓ Pipeline completed in {stopwatch.Elapsed.TotalSeconds:F2}s[/]");
        LogPipelineComplete(logger, stopwatch.Elapsed.TotalSeconds);

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting pipeline with {StepCount} steps")]
    private static partial void LogPipelineStart(ILogger logger, int stepCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pipeline step '{Step}' failed: {Error}")]
    private static partial void LogStepFailed(ILogger logger, string step, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pipeline completed in {Seconds:F2}s")]
    private static partial void LogPipelineComplete(ILogger logger, double seconds);
}
