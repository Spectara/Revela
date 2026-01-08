using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Base record for all gallery content items (images, markdown files, etc.).
/// </summary>
/// <remarks>
/// Uses System.Text.Json polymorphism for serialization.
/// The "type" discriminator property identifies the concrete type.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ImageContent), "image")]
[JsonDerivedType(typeof(MarkdownContent), "markdown")]
public abstract record GalleryContent
{
    /// <summary>
    /// Filename of the content item (without directory path).
    /// Used for sorting and identification.
    /// </summary>
    /// <example>"photo-001.jpg" or "intro.md"</example>
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    /// <summary>
    /// Relative path to the source file within the source directory.
    /// Used for locating the file during processing.
    /// </summary>
    /// <remarks>
    /// For regular gallery images, this equals the gallery path + filename.
    /// For filtered images from <c>_images</c>, this is the path within _images.
    /// Uses forward slashes for cross-platform consistency.
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
    /// Hash for change detection.
    /// Computed during image processing, empty until then.
    /// </summary>
    [JsonPropertyName("hash")]
    public string Hash { get; init; } = "";
}
