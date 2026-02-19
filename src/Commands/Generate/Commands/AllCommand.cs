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
/// Orchestrates all registered <see cref="IGenerateStep"/> implementations
/// in order. Each step handles its own console output (progress bars, panels, etc.).
/// </para>
/// <para>
/// Standard steps: scan → statistics → pages → images.
/// Plugins can register additional steps at any order.
/// </para>
/// </remarks>
internal sealed partial class AllCommand(
    ILogger<AllCommand> logger,
    IEnumerable<IGenerateStep> generateSteps)
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
    /// Executes the full generation pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var steps = generateSteps.OrderBy(s => s.Order).ToList();

        if (steps.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No pipeline steps registered.[/]");
            return 0;
        }

        // Build pipeline description
        var pipelineNames = string.Join(" → ", steps.Select(s => s.Name));
        AnsiConsole.MarkupLine($"[blue]Pipeline:[/] {pipelineNames}");
        AnsiConsole.WriteLine();

        LogPipelineStart(logger, steps.Count);
        var stopwatch = Stopwatch.StartNew();

        // Execute each step
        var stepNumber = 1;
        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AnsiConsole.MarkupLine($"[cyan]━━━ Step {stepNumber}/{steps.Count}: {step.Name} ━━━[/]");
            AnsiConsole.MarkupLine($"[dim]{step.Description}[/]");
            AnsiConsole.WriteLine();

            var result = await step.ExecuteAsync(cancellationToken);

            if (result != 0)
            {
                LogStepFailed(logger, step.Name);
                return result;
            }

            AnsiConsole.WriteLine();
            stepNumber++;
        }

        stopwatch.Stop();
        AnsiConsole.MarkupLine($"[green]✓ Pipeline completed in {stopwatch.Elapsed.TotalSeconds:F2}s[/]");
        LogPipelineComplete(logger, stopwatch.Elapsed.TotalSeconds);

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting pipeline with {StepCount} steps")]
    private static partial void LogPipelineStart(ILogger logger, int stepCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pipeline step '{Step}' failed")]
    private static partial void LogStepFailed(ILogger logger, string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pipeline completed in {Seconds:F2}s")]
    private static partial void LogPipelineComplete(ILogger logger, double seconds);
}
