using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Coordinates artifact invalidation across Revela core and loaded packages.
/// </summary>
/// <remarks>
/// All methods validate the registered graph first (unique owners, known dependencies,
/// no cycles, durable artifacts depend only on durable artifacts) and stop at the first
/// failing invalidator. Dependents are always invalidated before what they depend on.
/// </remarks>
public interface IArtifactLifecycle
{
    /// <summary>
    /// Invalidates all registered artifacts that transitively depend on the artifact
    /// about to be replaced. The artifact itself is left to its producer.
    /// </summary>
    ValueTask<OperationResult> PrepareToReplaceAsync(
        ArtifactId artifact,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one artifact and its transitive dependents, for the owner's own clean command.
    /// </summary>
    ValueTask<OperationResult> InvalidateAsync(
        ArtifactId artifact,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every registered artifact of the given kinds and their transitive dependents,
    /// for the generic clean commands.
    /// </summary>
    /// <param name="kinds"><see cref="ArtifactKind.Cache"/> and/or <see cref="ArtifactKind.Output"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="kinds"/> contains <see cref="ArtifactKind.Durable"/>.</exception>
    ValueTask<OperationResult> InvalidateAllAsync(
        IReadOnlyCollection<ArtifactKind> kinds,
        CancellationToken cancellationToken = default);
}
