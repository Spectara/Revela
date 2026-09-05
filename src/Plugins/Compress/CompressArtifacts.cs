using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Compress;

/// <summary>
/// Artifact identifiers published by the Compress plugin.
/// </summary>
public static class CompressArtifacts
{
    /// <summary>Pre-compressed Gzip and Brotli siblings of the rendered site.</summary>
    public static ArtifactId PrecompressedSite { get; } =
        new("spectara.revela.compress/precompressed-site");
}
