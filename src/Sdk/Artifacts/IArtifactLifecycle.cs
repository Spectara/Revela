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
    ValueTask<ArtifactInvalidationResult> PrepareToReplaceAsync(
        ArtifactId artifact,
        CancellationToken cancellationToken = default);
}
