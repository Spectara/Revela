using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Represents an image with its metadata and processing variants
/// </summary>
/// <remarks>
/// Properties are named to match template expectations (Lumina theme):
/// - id: the file name without extension (a fallback label, not unique across galleries)
/// - slug: Relative path segment identifying the image variants (e.g., "events/fireworks/029081").
///   URLs are built from it via the <c>variant_url</c> template helper, never by concatenation.
/// </remarks>
[RevelaTemplateModel]
internal sealed class Image
{
    /// <summary>
    /// Source path relative to the source folder with forward slashes (e.g. "Landscapes/sunset.jpg");
    /// the image processor's result carries the absolute input path.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Image file name without path and extension (e.g., "029081").
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Slugified path segment including gallery context for unique image output.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="SourcePath"/> via <see cref="UrlBuilder.ToImageSlug"/>.
    /// Includes gallery directory segments to prevent filename collisions
    /// across galleries (e.g., "events/fireworks/029081").
    /// For shared <c>_images/</c>, the prefix is stripped (e.g., "canon-landscape-001").
    /// This is identity only — a context-free path segment, never a full URL.
    /// URLs to the variants are built via the <c>variant_url</c> template helper.
    /// </remarks>
    public required string Slug { get; init; }

    /// <summary>
    /// The file name without extension, e.g. for a fallback label (<c>t 'photo.label' image.id</c>).
    /// </summary>
    /// <remarks>
    /// Not slugified (may contain spaces or capitals) and not unique across galleries; HTML ids
    /// are built from <see cref="Slug"/> instead.
    /// </remarks>
    public string Id => FileName;

    public required int Width { get; init; }
    public required int Height { get; init; }
    public long FileSize { get; init; }
    public DateTime DateTaken { get; init; }
    public ExifData? Exif { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// Keywords from the photo's XMP metadata (<c>image.keywords</c> in templates).
    /// </summary>
    /// <remarks>
    /// Often workflow markers (e.g. "Selected"), so themes should not publish them by default.
    /// </remarks>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>
    /// Star rating from XMP (-1 rejected, 0 unrated, 1–5); <c>null</c> when absent.
    /// </summary>
    public int? Rating { get; init; }

    /// <summary>
    /// Variant files written by the image processor (counts and sizes for progress reporting).
    /// </summary>
    /// <remarks>
    /// Only filled on the processor's result; pages are rendered from the manifest, so templates
    /// would always see an empty list and never get it.
    /// </remarks>
    [ScriptIgnore]
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
    /// Average colour of the photo as lowercase sRGB hex (<c>#rrggbb</c>).
    /// </summary>
    /// <remarks>
    /// Computed during scan. Themes paint it while the photo loads, e.g.
    /// <c>style="--image-color:{{ image.color }}"</c>. <c>null</c> when unknown.
    /// </remarks>
    public string? Color { get; init; }

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
            Slug = UrlBuilder.ToImageSlug(sourcePath),
            Width = entry.Width,
            Height = entry.Height,
            FileSize = entry.FileSize,
            DateTaken = entry.DateTaken ?? DateTime.MinValue,
            Exif = entry.Exif,
            Title = entry.Title,
            Description = entry.Description,
            Keywords = entry.Keywords,
            Rating = entry.Rating,
            Sizes = entry.Sizes,
            Color = entry.Color
        };
    }
}

/// <summary>
/// Represents a processed variant of an image (different size/format)
/// </summary>
[RevelaTemplateModel]
internal sealed class ImageVariant
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string Format { get; init; }
    public required string Path { get; init; }
    public long Size { get; init; }
}

