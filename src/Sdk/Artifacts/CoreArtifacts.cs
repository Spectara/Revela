using System.Collections.Frozen;

namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Artifact identifiers produced by Revela's built-in generation services.
/// </summary>
public static class CoreArtifacts
{
    /// <summary>The scanned content manifest.</summary>
    public static ArtifactId Manifest { get; } = new("revela.core/manifest");

    /// <summary>The rendered site, including pages, assets, and static files.</summary>
    public static ArtifactId RenderedSite { get; } = new("revela.core/rendered-site");

    /// <summary>Processed image variants in the output directory.</summary>
    public static ArtifactId ProcessedImages { get; } = new("revela.core/processed-images");

    /// <summary>All artifact roots owned by Revela Core.</summary>
    public static IReadOnlySet<ArtifactId> All { get; } = new HashSet<ArtifactId>
    {
        Manifest,
        RenderedSite,
        ProcessedImages
    }.ToFrozenSet();
}
