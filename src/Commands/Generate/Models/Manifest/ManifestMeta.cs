using System.Text.Json.Serialization;

namespace Spectara.Revela.Commands.Generate.Models.Manifest;

/// <summary>
/// Manifest metadata for tracking configuration changes.
/// </summary>
public sealed class ManifestMeta
{
    /// <summary>
    /// Manifest schema version for future migrations.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    /// <summary>
    /// Hash of image processing configuration (sizes, formats, quality).
    /// When this changes, all images need to be regenerated.
    /// </summary>
    [JsonPropertyName("configHash")]
    public string ConfigHash { get; set; } = string.Empty;

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
