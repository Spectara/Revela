using System.CommandLine;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Calendar.Commands;

/// <summary>
/// Removes the calendar data (<see cref="CalendarArtifacts.Data"/>) through its invalidator,
/// the same one <c>revela clean cache</c> uses.
/// </summary>
internal sealed class CleanCalendarCommand(IArtifactLifecycle artifactLifecycle) : IPipelineStep
{
    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "calendar";

    ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken) =>
        artifactLifecycle.InvalidateAsync(CalendarArtifacts.Data, cancellationToken);

    // ── CLI command ──

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("calendar", "Remove the calendar data files (rebuilt by 'generate calendar')");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    /// <summary>
    /// Removes the calendar data and reports the result.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success, 1 = a file could not be deleted).</returns>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await artifactLifecycle.InvalidateAsync(CalendarArtifacts.Data, cancellationToken);
        if (result.Success)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Removed calendar data");
            return 0;
        }

        AnsiConsole.MarkupLine($"{OutputMarkers.Error} {Markup.Escape(result.ErrorMessage ?? "Could not remove calendar data")}");
        return 1;
    }
}
