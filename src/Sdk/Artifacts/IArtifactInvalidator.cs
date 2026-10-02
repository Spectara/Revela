using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Declares and removes one derived artifact owned by a loaded package.
/// </summary>
/// <remarks>
/// Register implementations as enumerable services. Before an input artifact is
/// replaced, Revela invalidates all transitive dependents from leaves to roots.
/// </remarks>
public interface IArtifactInvalidator
{
    /// <summary>The derived artifact owned by this invalidator.</summary>
    ArtifactId Artifact { get; }

    /// <summary>Artifacts from which <see cref="Artifact"/> is derived.</summary>
    IReadOnlyCollection<ArtifactId> DependsOn { get; }

    /// <summary>Removes the derived artifact.</summary>
    ValueTask<OperationResult> InvalidateAsync(
        CancellationToken cancellationToken = default);
}
