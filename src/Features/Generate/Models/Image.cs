namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Represents an image with its metadata and processing variants
/// </summary>
/// <remarks>
/// Properties are named to match template expectations (Expose theme):
/// - id: Unique identifier for HTML anchors (filename without extension)
/// - url: Relative path to image variants (e.g., "photo1" -> images/photo1/640.jpg)
/// </remarks>
public sealed class Image
{
    public required string SourcePath { get; init; }
    public required string FileName { get; init; }

    /// <summary>
    /// Unique identifier for HTML anchors and lightbox targets
    /// </summary>
    /// <remarks>
    /// Typically the filename without extension, URL-safe.
    /// Used in templates as: id="{{ image.id }}"
    /// </remarks>
    public string Id => FileName;

    /// <summary>
    /// Relative path segment for image variants
    /// </summary>
    /// <remarks>
    /// Used in templates to construct paths like: {{ resource_path }}{{ image.url }}/640.jpg
    /// The template combines this with resource_path (e.g., "images/") and variant size.
    /// This is NOT a full URI - it's a path segment (e.g., "photo1").
    /// </remarks>
#pragma warning disable CA1056 // URI-like properties should not be strings - this is a path segment, not a URI
    public string Url => FileName;
#pragma warning restore CA1056

    public required int Width { get; init; }
    public required int Height { get; init; }
    public long FileSize { get; init; }
    public DateTime DateTaken { get; init; }
    public ExifData? Exif { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<ImageVariant> Variants { get; init; } = [];
}

/// <summary>
/// Represents a processed variant of an image (different size/format)
/// </summary>
public sealed class ImageVariant
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string Format { get; init; }
    public required string Path { get; init; }
    public long Size { get; init; }
}
