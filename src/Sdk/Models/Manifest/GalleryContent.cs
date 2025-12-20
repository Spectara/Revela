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
