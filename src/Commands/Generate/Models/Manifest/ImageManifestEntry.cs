using System.Text.Json.Serialization;

namespace Spectara.Revela.Commands.Generate.Models.Manifest;

/// <summary>
/// Manifest entry for a single source image.
/// </summary>
public sealed class ImageManifestEntry
{
    /// <summary>
    /// Filename of the source image (without directory path).
    /// </summary>
    /// <example>"photo-001.jpg"</example>
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    /// <summary>
    /// Hash of source image for change detection.
    /// Format: MD5({filename}_{lastWriteTime}_{fileSize})[0..12]
    /// </summary>
    [JsonPropertyName("hash")]
    public required string Hash { get; init; }

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
    /// Original file size in bytes.
    /// </summary>
    [JsonPropertyName("fileSize")]
    public long FileSize { get; init; }

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
    /// Timestamp when this entry was last processed.
    /// </summary>
    [JsonPropertyName("processedAt")]
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}
