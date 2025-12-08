using System.Text.Json.Serialization;

namespace Spectara.Revela.Commands.Generate.Models.Manifest;

/// <summary>
/// Manifest entry for a single source image.
/// </summary>
public sealed class ImageManifestEntry
{
    /// <summary>
    /// Hash of source image for change detection.
    /// Format: MD5({filename}_{lastWriteTime}_{fileSize})[0..12]
    /// </summary>
    [JsonPropertyName("hash")]
    public required string Hash { get; init; }

    /// <summary>
    /// Original image width in pixels.
    /// </summary>
    [JsonPropertyName("originalWidth")]
    public required int OriginalWidth { get; init; }

    /// <summary>
    /// Original image height in pixels.
    /// </summary>
    [JsonPropertyName("originalHeight")]
    public required int OriginalHeight { get; init; }

    /// <summary>
    /// List of generated image widths (e.g., [320, 640, 1024, 1920]).
    /// Smaller images are skipped if original is too small.
    /// </summary>
    [JsonPropertyName("generatedSizes")]
    public required IReadOnlyList<int> GeneratedSizes { get; init; }

    /// <summary>
    /// List of generated formats (e.g., ["jpg", "webp"]).
    /// </summary>
    [JsonPropertyName("generatedFormats")]
    public required IReadOnlyList<string> GeneratedFormats { get; init; }

    /// <summary>
    /// Output path relative to images directory (e.g., "01-events/photo-001").
    /// </summary>
    [JsonPropertyName("outputPath")]
    public required string OutputPath { get; init; }

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
