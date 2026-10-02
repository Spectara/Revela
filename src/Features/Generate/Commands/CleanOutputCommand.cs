using System.CommandLine;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// <c>revela clean output</c>: removes every <see cref="ArtifactKind.Output"/> artifact through
/// its owner — the rendered site and processed images (with the image processing state), and
/// plugin output such as pre-compressed sidecars with their ownership record.
/// </summary>
/// <remarks>
/// Refuses an output path that is a filesystem root or that is, or contains, the project,
/// source or home directory, before anything is deleted.
/// </remarks>
internal sealed class CleanOutputCommand(KindClean kindClean) : IPipelineStep
{
    private static readonly ArtifactKind[] Kinds = [ArtifactKind.Output];

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "output";

    async ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken) =>
        await kindClean.RunAsync(Kinds, cancellationToken);

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("output", "Remove the generated site and what describes it (image state, compression record)");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    public Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        kindClean.RunCommandAsync(Kinds, "Removed the output", cancellationToken);
}
