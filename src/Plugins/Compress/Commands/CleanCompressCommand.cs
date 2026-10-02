using System.CommandLine;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Compress.Commands;

/// <summary>
/// Removes the pre-compressed sidecars and their ownership record through the same
/// invalidator <c>revela clean output</c> uses.
/// </summary>
internal sealed class CleanCompressCommand(IArtifactLifecycle artifactLifecycle) : IPipelineStep
{
    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "compress";

    ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken) =>
        artifactLifecycle.InvalidateAsync(CompressArtifacts.PrecompressedSite, cancellationToken);

    // ── CLI command ──

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("compress", "Remove the .gz/.br files created by 'generate compress' from output");

        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(cancellationToken));

        return command;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await artifactLifecycle.InvalidateAsync(CompressArtifacts.PrecompressedSite, cancellationToken);
        if (result.Success)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Removed compressed files");
            return 0;
        }

        AnsiConsole.MarkupLine($"{OutputMarkers.Error} {Markup.Escape(result.ErrorMessage ?? "Could not remove compressed files")}");
        return 1;
    }
}
