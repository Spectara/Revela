using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Content item representing an image file.
/// </summary>
public sealed record ImageContent : GalleryContent
{
    /// <summary>
    /// Image width in pixels.
    /// </summary>
    [JsonPropertyName("width")]
    public required int Width { get; init; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    [JsonPropertyName("height")]
    public required int Height { get; init; }

    /// <summary>
    /// List of sizes to generate (widths in pixels).
    /// Calculated from config, filtered by actual image width.
    /// </summary>
    /// <example>[320, 640, 1024, 1920]</example>
    [JsonPropertyName("sizes")]
    public required IReadOnlyList<int> Sizes { get; init; }

    /// <summary>
    /// Date the photo was taken (from EXIF or file date).
    /// </summary>
    [JsonPropertyName("dateTaken")]
    public DateTime? DateTaken { get; init; }

    /// <summary>
    /// EXIF metadata extracted from the image.
    /// </summary>
    [JsonPropertyName("exif")]
    public ExifData? Exif { get; init; }

    /// <summary>
    /// Timestamp when this image was last processed.
    /// </summary>
    [JsonPropertyName("processedAt")]
    public DateTime ProcessedAt { get; init; }

    /// <summary>
    /// Placeholder for lazy loading (CSS-only LQIP hash)
    /// </summary>
    /// <remarks>
    /// Contains a 20-bit integer as string (e.g., "-721311") that CSS decodes
    /// into 6 radial gradients over a base color. <c>null</c> when placeholder
    /// generation is disabled.
    /// Used in templates: <c>style="--lqip:{{ image.placeholder }}"</c>
    /// </remarks>
    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }
}
