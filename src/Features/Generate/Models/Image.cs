using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Represents an image with its metadata and processing variants
/// </summary>
/// <remarks>
/// Properties are named to match template expectations (Lumina theme):
/// - id: Unique identifier for HTML anchors (filename without extension)
/// - url: Relative path to image variants (e.g., "events/fireworks/029081" -> images/events/fireworks/029081/640.jpg)
/// </remarks>
internal sealed class Image
{
    /// <summary>
    /// Full path to the source image file.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Image filename without path (e.g., "photo1.jpg").
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Slugified path including gallery context for unique image output.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="SourcePath"/> via <see cref="UrlBuilder.ToImageSlug"/>.
    /// Includes gallery directory segments to prevent filename collisions
    /// across galleries (e.g., "events/fireworks/029081").
    /// For shared <c>_images/</c>, the prefix is stripped (e.g., "canon-landscape-001").
    /// </remarks>
    public required string ImageSlug { get; init; }

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
    /// Used in templates to construct paths like: {{ image_basepath }}{{ image.url }}/640.jpg
    /// Includes gallery path to prevent collisions (e.g., "events/fireworks/029081").
    /// This is NOT a full URI — it's a path segment.
    /// Typed as <see cref="RelativePath"/> to distinguish from real URLs (avoids CA1056).
    /// </remarks>
    public RelativePath Url => ImageSlug;

    public required int Width { get; init; }
    public required int Height { get; init; }
    public long FileSize { get; init; }
    public DateTime DateTaken { get; init; }
    public ExifData? Exif { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<ImageVariant> Variants { get; init; } = [];

    /// <summary>
    /// List of available image widths (for dynamic srcset in templates).
    /// </summary>
    /// <remarks>
    /// Contains only sizes that were actually generated.
    /// Small images may skip larger sizes if original is too small.
    /// Used in templates: {{ for size in image.sizes }}...{{ end }}
    /// </remarks>
    public IReadOnlyList<int> Sizes { get; init; } = [];

    /// <summary>
    /// Placeholder CSS value for lazy loading
    /// </summary>
    /// <remarks>
    /// Contains a CSS-only LQIP hash (20-bit integer as string, e.g., "-721311")
    /// that CSS decodes into 6 radial gradients over a base color.
    /// <c>null</c> when placeholder generation is disabled.
    /// Used in templates: <c>style="--lqip:{{ image.placeholder }}"</c>
    /// </remarks>
    public string? Placeholder { get; init; }

    /// <summary>
    /// Create an Image from a manifest entry (for cache hits).
    /// </summary>
    /// <param name="sourcePath">Full path to source image</param>
    /// <param name="entry">Manifest entry with cached metadata</param>
    /// <returns>Image populated from manifest data</returns>
    public static Image FromManifestEntry(string sourcePath, ImageContent entry)
    {
        return new Image
        {
            SourcePath = sourcePath,
            FileName = Path.GetFileNameWithoutExtension(entry.Filename),
            ImageSlug = UrlBuilder.ToImageSlug(sourcePath),
            Width = entry.Width,
            Height = entry.Height,
            FileSize = entry.FileSize,
            DateTaken = entry.DateTaken ?? DateTime.MinValue,
            Exif = entry.Exif,
            Sizes = entry.Sizes,
            Placeholder = entry.Placeholder
        };
    }
}

/// <summary>
/// Represents a processed variant of an image (different size/format)
/// </summary>
internal sealed class ImageVariant
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string Format { get; init; }
    public required string Path { get; init; }
    public long Size { get; init; }
}

