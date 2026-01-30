using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Manifest metadata for tracking configuration changes.
/// </summary>
public sealed class ManifestMeta
{
    /// <summary>
    /// Manifest schema version for future migrations.
    /// </summary>
    /// <remarks>
    /// Version history:
    /// - v1: Initial version with separate galleries and images
    /// - v2: Added navigation tree
    /// - v3: Unified tree structure with root node containing everything
    /// - v4: Polymorphic content list (images + markdown), renamed images to content
    /// </remarks>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 4;

    /// <summary>
    /// Hash of image processing configuration (sizes, formats, quality).
    /// When this changes, all images need to be regenerated.
    /// </summary>
    [JsonPropertyName("configHash")]
    public string ConfigHash { get; set; } = string.Empty;

    /// <summary>
    /// Hash of scan configuration (placeholder strategy, min dimensions).
    /// When this changes, all metadata needs to be re-read from source files.
    /// </summary>
    [JsonPropertyName("scanConfigHash")]
    public string ScanConfigHash { get; set; } = string.Empty;

    /// <summary>
    /// Format qualities used for last image generation.
    /// Key = format (jpg, webp, avif), Value = quality (1-100).
    /// When a quality changes, all images of that format need regeneration.
    /// </summary>
    [JsonPropertyName("formatQualities")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON deserialization")]
    public Dictionary<string, int> FormatQualities { get; set; } = [];

    /// <summary>
    /// Timestamp of last content scan.
    /// </summary>
    [JsonPropertyName("lastScanned")]
    public DateTime? LastScanned { get; set; }

    /// <summary>
    /// Timestamp of last image processing.
    /// </summary>
    [JsonPropertyName("lastImagesProcessed")]
    public DateTime? LastImagesProcessed { get; set; }

    /// <summary>
    /// Timestamp of last manifest update (any change).
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
