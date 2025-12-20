using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Site manifest for incremental builds and caching.
/// </summary>
/// <remarks>
/// <para>
/// The manifest stores metadata about all processed content in a unified tree structure.
/// The root node represents the home page and contains the entire site hierarchy.
/// </para>
/// <para>
/// Enables:
/// - Skip unchanged images (hash comparison)
/// - Provide gallery/navigation data without re-scanning
/// - Dynamic srcset based on actually generated sizes
/// - EXIF data caching (replaces separate ExifCache)
/// </para>
/// <para>Location: .cache/manifest.json</para>
/// </remarks>
public sealed class ImageManifest
{
    /// <summary>
    /// Manifest metadata for version and configuration tracking.
    /// </summary>
    [JsonPropertyName("_meta")]
    public ManifestMeta Meta { get; set; } = new();

    /// <summary>
    /// Root node of the site tree (home page).
    /// </summary>
    /// <remarks>
    /// The entire site structure is represented as a tree starting from this root.
    /// Galleries with images have a non-null slug, branch nodes have null slug.
    /// </remarks>
    [JsonPropertyName("root")]
    public ManifestEntry? Root { get; set; }
}
