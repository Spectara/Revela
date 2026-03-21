using System.CommandLine;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Generate.Commands;

/// <summary>
/// Executes all registered clean steps in order.
/// </summary>
/// <remarks>
/// Orchestrates all registered <see cref="ICleanStep"/> implementations
/// in order. Each step handles its own console output.
/// Plugins can register additional steps at any order.
/// </remarks>
internal sealed partial class CleanAllCommand(
    ILogger<CleanAllCommand> logger,
    IEnumerable<ICleanStep> cleanSteps)
{
    /// <summary>Order for this command (first in menu).</summary>
    public const int Order = 0;

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var stepNames = string.Join(" → ", cleanSteps.OrderBy(s => s.Order).Select(s => s.Name));
        var description = stepNames.Length > 0
            ? $"Clean all ({stepNames})"
            : "Clean all generated files";

        var command = new Command("all", description);

        command.SetAction(async (_, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    /// <summary>
    /// Executes all clean steps in order.
    /// </summary>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var steps = cleanSteps.OrderBy(s => s.Order).ToList();

        if (steps.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No clean steps registered.[/]");
            return 0;
        }

        var pipelineNames = string.Join(" → ", steps.Select(s => s.Name));
        AnsiConsole.MarkupLine($"[blue]Pipeline:[/] [dim]{pipelineNames}[/]");
        AnsiConsole.WriteLine();

        LogPipelineStart(logger, steps.Count);

        var stepNumber = 1;

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AnsiConsole.MarkupLine($"[cyan]━━━ Step {stepNumber}/{steps.Count}: {Markup.Escape(step.Name)} ━━━[/]");

            var result = await step.ExecuteAsync(cancellationToken);
            if (result != 0)
            {
                LogStepFailed(logger, step.Name, result);
                return result;
            }

            stepNumber++;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"{OutputMarkers.Success} All clean operations completed");

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting clean pipeline with {Count} step(s)")]
    private static partial void LogPipelineStart(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Clean step '{Name}' failed with exit code {ExitCode}")]
    private static partial void LogStepFailed(ILogger logger, string name, int exitCode);
}
