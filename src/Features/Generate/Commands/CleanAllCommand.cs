using System.CommandLine;

using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// <c>revela clean all</c>: removes every <see cref="ArtifactKind.Cache"/> and
/// <see cref="ArtifactKind.Output"/> artifact through its owner.
/// </summary>
/// <remarks>
/// <see cref="ArtifactKind.Durable"/> artifacts (data that is expensive or impossible to
/// reproduce) stay; only their owner's own clean command removes them. Registered explicitly,
/// so the host does not generate an <c>all</c> that runs every clean subcommand.
/// </remarks>
internal sealed class CleanAllCommand(KindClean kindClean)
{
    private static readonly ArtifactKind[] Kinds = [ArtifactKind.Cache, ArtifactKind.Output];

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("all", "Remove everything Revela can rebuild: cache and output (keeps durable plugin data)");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    public Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        kindClean.RunCommandAsync(Kinds, "Removed cache and output", cancellationToken);
}
