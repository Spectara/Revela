using System.CommandLine;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Statistics.Commands;

/// <summary>
/// Removes the statistics data (<see cref="StatisticsArtifacts.Data"/>) through its invalidator,
/// the same one <c>revela clean cache</c> uses.
/// </summary>
internal sealed class CleanStatisticsCommand(IArtifactLifecycle artifactLifecycle) : IPipelineStep
{
    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "statistics";

    ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken) =>
        artifactLifecycle.InvalidateAsync(StatisticsArtifacts.Data, cancellationToken);

    // ── CLI command ──

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("statistics", "Remove the statistics data files (rebuilt by 'generate statistics')");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    /// <summary>
    /// Removes the statistics data and reports the result.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success, 1 = a file could not be deleted).</returns>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await artifactLifecycle.InvalidateAsync(StatisticsArtifacts.Data, cancellationToken);
        if (result.Success)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Removed statistics data");
            return 0;
        }

        AnsiConsole.MarkupLine($"{OutputMarkers.Error} {Markup.Escape(result.ErrorMessage ?? "Could not remove statistics data")}");
        return 1;
    }
}
