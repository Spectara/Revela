using System.Collections.Frozen;

namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Artifact identifiers produced by Revela's built-in generation services.
/// </summary>
/// <remarks>
/// Revela core registers an <see cref="IArtifactInvalidator"/> for each of them, like a plugin
/// would. None depends on another: the rendered site and processed images are made from the
/// source files, not from the manifest file, so cleaning the cache keeps the output.
/// </remarks>
public static class CoreArtifacts
{
    /// <summary>The owner name of Revela core, and its folder <c>.revela/core/</c>.</summary>
    public const string Owner = "core";

    /// <summary>The scanned content manifest (<see cref="ArtifactKind.Cache"/>).</summary>
    public static ArtifactId Manifest { get; } = new("core/manifest");

    /// <summary>
    /// The rendered site: pages, assets and static files in the output
    /// (<see cref="ArtifactKind.Output"/>).
    /// </summary>
    public static ArtifactId RenderedSite { get; } = new("core/rendered-site");

    /// <summary>
    /// Processed image variants in the output and the record of how they were made
    /// (<see cref="ArtifactKind.Output"/>).
    /// </summary>
    public static ArtifactId ProcessedImages { get; } = new("core/processed-images");

    /// <summary>All artifacts owned by Revela core.</summary>
    public static IReadOnlySet<ArtifactId> All { get; } = new HashSet<ArtifactId>
    {
        Manifest,
        RenderedSite,
        ProcessedImages
    }.ToFrozenSet();
}
