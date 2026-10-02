using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Compress;

/// <summary>
/// Artifact identifiers published by the Compress plugin.
/// </summary>
public static class CompressArtifacts
{
    /// <summary>
    /// Pre-compressed Gzip and Brotli siblings of the rendered site, with the record of which
    /// files Revela created (<c>.revela/compress/ownership.json</c>); <see cref="ArtifactKind.Output"/>.
    /// </summary>
    public static ArtifactId PrecompressedSite { get; } =
        new("compress/precompressed-site");
}
