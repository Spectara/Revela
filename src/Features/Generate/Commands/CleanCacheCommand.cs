using System.CommandLine;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// <c>revela clean cache</c>: removes every <see cref="ArtifactKind.Cache"/> artifact
/// (scan manifest, plugin data files) through its owner.
/// </summary>
/// <remarks>
/// The output and what describes it (image processing state, compression record) are
/// <see cref="ArtifactKind.Output"/> artifacts and stay, so the next build re-encodes nothing.
/// </remarks>
internal sealed class CleanCacheCommand(KindClean kindClean) : IPipelineStep
{
    private static readonly ArtifactKind[] Kinds = [ArtifactKind.Cache];

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "cache";

    async ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken) =>
        await kindClean.RunAsync(Kinds, cancellationToken);

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("cache", "Remove data Revela can rebuild (scan, plugin data); keeps the output and image state");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    public Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        kindClean.RunCommandAsync(Kinds, "Removed cached data", cancellationToken);
}
