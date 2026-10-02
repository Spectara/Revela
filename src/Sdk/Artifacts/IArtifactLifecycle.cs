using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Coordinates invalidation before a generated artifact is replaced.
/// </summary>
public interface IArtifactLifecycle
{
    /// <summary>
    /// Invalidates all loaded artifacts that transitively depend on the artifact
    /// about to be replaced.
    /// </summary>
    ValueTask<OperationResult> PrepareToReplaceAsync(
        ArtifactId artifact,
        CancellationToken cancellationToken = default);
}
