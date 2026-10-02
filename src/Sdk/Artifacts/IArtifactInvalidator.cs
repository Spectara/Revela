using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Declares and removes one artifact owned by Revela core or a loaded package.
/// </summary>
/// <remarks>
/// <para>
/// Register implementations as enumerable services. Every file a package keeps in its folder
/// (<see cref="ProjectPaths.GetOwnerDirectory"/>) or writes into the output belongs to exactly
/// one artifact, so the clean commands can remove it.
/// </para>
/// <para>
/// A <see cref="DependsOn"/> edge means "must be invalidated when the dependency is replaced
/// or invalidated". Before an artifact is replaced, Revela invalidates its transitive
/// dependents from leaves to roots; generic clean commands do the same for every artifact
/// of the selected <see cref="Kind"/>. Only declare an edge when the dependency's new version
/// makes this artifact wrong; reading a file is not enough (rendered pages read plugin data,
/// but stay valid until they are rendered again).
/// </para>
/// </remarks>
public interface IArtifactInvalidator
{
    /// <summary>The artifact owned by this invalidator.</summary>
    ArtifactId Artifact { get; }

    /// <summary>
    /// How long the artifact lives, which decides the clean commands that remove it.
    /// </summary>
    ArtifactKind Kind { get; }

    /// <summary>
    /// Artifacts whose replacement makes <see cref="Artifact"/> wrong. A <see cref="ArtifactKind.Durable"/>
    /// artifact may only depend on other durable artifacts.
    /// </summary>
    IReadOnlyCollection<ArtifactId> DependsOn { get; }

    /// <summary>Removes the artifact completely (a missing artifact is success).</summary>
    ValueTask<OperationResult> InvalidateAsync(
        CancellationToken cancellationToken = default);
}
