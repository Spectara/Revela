using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Manifest metadata for tracking configuration changes.
/// </summary>
/// <remarks>
/// Immutable: all properties are <c>init</c>-only. To update metadata, use the
/// <c>with</c> expression to produce a modified copy.
/// </remarks>
public sealed record ManifestMeta
{
    /// <summary>
    /// Manifest schema version for future migrations.
    /// </summary>
    /// <remarks>
    /// Version history:
    /// <list type="bullet">
    ///   <item><description>v1: Initial version with separate galleries and images</description></item>
    ///   <item><description>v2: Added navigation tree</description></item>
    ///   <item><description>v3: Unified tree structure with root node containing everything</description></item>
    ///   <item><description>v4: Polymorphic content list (images + markdown), renamed images to content</description></item>
    /// </list>
    /// </remarks>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 4;

    /// <summary>
    /// Hash of image processing configuration (sizes, formats, quality).
    /// When this changes, all images need to be regenerated.
    /// </summary>
    [JsonPropertyName("configHash")]
    public string ConfigHash { get; init; } = string.Empty;

    /// <summary>
    /// Hash of scan configuration (placeholder strategy, min dimensions).
    /// When this changes, all metadata needs to be re-read from source files.
    /// </summary>
    [JsonPropertyName("scanConfigHash")]
    public string ScanConfigHash { get; init; } = string.Empty;

    /// <summary>
    /// Format qualities used for last image generation.
    /// Key = format (jpg, webp, avif), Value = quality (1-100).
    /// When a quality changes, all images of that format need regeneration.
    /// </summary>
    [JsonPropertyName("formatQualities")]
    public IReadOnlyDictionary<string, int> FormatQualities { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Timestamp of last content scan.
    /// </summary>
    [JsonPropertyName("lastScanned")]
    public DateTime? LastScanned { get; init; }

    /// <summary>
    /// Timestamp of last image processing.
    /// </summary>
    [JsonPropertyName("lastImagesProcessed")]
    public DateTime? LastImagesProcessed { get; init; }

    /// <summary>
    /// Timestamp of last manifest update (any change).
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}
