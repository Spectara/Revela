using System.Text.Json.Serialization;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Image manifest for incremental builds and caching.
/// </summary>
/// <remarks>
/// The manifest stores metadata about all processed images, enabling:
/// - Skip unchanged images (hash comparison)
/// - Provide image data without loading files (--skip-images mode)
/// - Dynamic srcset based on actually generated sizes
/// - EXIF data caching (replaces separate ExifCache)
///
/// Location: .cache/manifest.json
/// </remarks>
public sealed class ImageManifest
{
    /// <summary>
    /// Manifest metadata for version and configuration tracking.
    /// </summary>
    [JsonPropertyName("_meta")]
    public ManifestMeta Meta { get; set; } = new();

    /// <summary>
    /// Image entries keyed by source path (relative to source directory).
    /// </summary>
    /// <example>
    /// "01 Events/photo-001.jpg" → ImageManifestEntry
    /// </example>
    [JsonPropertyName("images")]
    public Dictionary<string, ImageManifestEntry> Images { get; init; } = [];
}

/// <summary>
/// Manifest metadata for tracking configuration changes.
/// </summary>
public sealed class ManifestMeta
{
    /// <summary>
    /// Manifest schema version for future migrations.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Hash of image processing configuration (sizes, formats, quality).
    /// When this changes, all images need to be regenerated.
    /// </summary>
    [JsonPropertyName("configHash")]
    public string ConfigHash { get; set; } = string.Empty;

    /// <summary>
    /// Theme ID used for generation.
    /// Different themes may require different image sizes.
    /// </summary>
    [JsonPropertyName("themeId")]
    public string? ThemeId { get; set; }

    /// <summary>
    /// Timestamp of last manifest update.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

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
