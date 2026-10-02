using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// An image of a page in the manifest, with the metadata read during scan.
/// </summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed record ImageContent
{
    /// <summary>
    /// Filename of the image (without directory path).
    /// </summary>
    /// <example>"photo-001.jpg"</example>
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    /// <summary>
    /// Relative path to the source file within the source directory, with forward slashes.
    /// </summary>
    /// <remarks>
    /// For folder images this is the folder path + filename; for images selected by a filter
    /// (e.g. from <c>_images</c>) it is the path of the matched file.
    /// </remarks>
    /// <example>"_images/canon-night-001.jpg" or "01 Gallery/photo-001.jpg"</example>
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; init; } = "";

    /// <summary>
    /// File size in bytes.
    /// </summary>
    [JsonPropertyName("fileSize")]
    public long FileSize { get; init; }
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
    /// Photo title: XMP <c>dc:title</c>, falling back to the EXIF <c>XPTitle</c> tag.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Photo description: XMP <c>dc:description</c>, falling back to the EXIF
    /// <c>ImageDescription</c> tag.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Keywords from XMP <c>dc:subject</c> (e.g. written by Capture One or Lightroom).
    /// </summary>
    [JsonPropertyName("keywords")]
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>
    /// Star rating from XMP <c>xmp:Rating</c>: -1 (rejected), 0 (unrated) to 5;
    /// <c>null</c> when the file carries no rating.
    /// </summary>
    [JsonPropertyName("rating")]
    public int? Rating { get; init; }

    /// <summary>
    /// Last modification time of the source file.
    /// Used for cache invalidation during scan.
    /// </summary>
    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; init; }

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
