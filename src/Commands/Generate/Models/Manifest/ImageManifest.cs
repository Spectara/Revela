using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Spectara.Revela.Commands.Generate.Models.Manifest;

/// <summary>
/// Image manifest for incremental builds and caching.
/// </summary>
/// <remarks>
/// The manifest stores metadata about all processed content, enabling:
/// - Skip unchanged images (hash comparison)
/// - Provide gallery/navigation data without re-scanning
/// - Dynamic srcset based on actually generated sizes
/// - EXIF data caching (replaces separate ExifCache)
///
/// Location: .cache/manifest.json
/// </remarks>
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Required for JSON deserialization")]
public sealed class ImageManifest
{
    /// <summary>
    /// Manifest metadata for version and configuration tracking.
    /// </summary>
    [JsonPropertyName("_meta")]
    public ManifestMeta Meta { get; set; } = new();

    /// <summary>
    /// Gallery entries from content scan.
    /// </summary>
    [JsonPropertyName("galleries")]
    public List<GalleryManifestEntry> Galleries { get; init; } = [];

    /// <summary>
    /// Navigation tree from content scan.
    /// </summary>
    [JsonPropertyName("navigation")]
    public List<NavigationManifestEntry> Navigation { get; init; } = [];

    /// <summary>
    /// Image entries keyed by source path (relative to source directory).
    /// </summary>
    /// <example>
    /// "01 Events/photo-001.jpg" → ImageManifestEntry
    /// </example>
    [JsonPropertyName("images")]
    public Dictionary<string, ImageManifestEntry> Images { get; init; } = [];
}
