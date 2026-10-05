using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Features.Generate.Abstractions;

/// <summary>
/// Abstraction for image processing operations
/// </summary>
/// <remarks>
/// Implementations handle:
/// - Resizing images to multiple sizes
/// - Converting to multiple formats (WebP, JPG, AVIF)
/// - EXIF extraction
/// - Quality control
/// </remarks>
internal interface IImageProcessor
{
    /// <summary>
    /// Process a single image: resize, convert formats, extract EXIF
    /// </summary>
    /// <param name="inputPath">Path to the source image</param>
    /// <param name="options">Processing options (sizes, formats, quality)</param>
    /// <param name="onVariantProgress">Callback invoked for each variant lifecycle event (Started before encode, Done after write, Skipped from cache). Format: jpg/webp/avif/png.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed image with variants and EXIF data</returns>
    Task<Image> ProcessImageAsync(
        string inputPath,
        ImageProcessingOptions options,
        Action<VariantState, string>? onVariantProgress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read image metadata without processing (fast operation).
    /// </summary>
    /// <remarks>
    /// Reads the image header for dimensions, EXIF and XMP, and computes the photo's average
    /// colour and content gamut from a small shrink-on-load thumbnail rather than the
    /// full-resolution image.
    /// </remarks>
    /// <param name="inputPath">Path to the source image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Image metadata (dimensions, EXIF, file info, average colour, gamut)</returns>
    Task<ImageMetadata> ReadMetadataAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Image metadata extracted without full processing.
/// </summary>
internal sealed class ImageMetadata
{
    /// <summary>Image width in pixels</summary>
    public required int Width { get; init; }

    /// <summary>Image height in pixels</summary>
    public required int Height { get; init; }

    /// <summary>File size in bytes</summary>
    public required long FileSize { get; init; }

    /// <summary>EXIF metadata (may be null if extraction fails)</summary>
    public ExifData? Exif { get; init; }

    /// <summary>Date photo was taken (from EXIF or file date)</summary>
    public DateTime? DateTaken { get; init; }

    /// <summary>Title (XMP <c>dc:title</c>, else EXIF <c>XPTitle</c>)</summary>
    public string? Title { get; init; }

    /// <summary>Description (XMP <c>dc:description</c>, else EXIF <c>ImageDescription</c>)</summary>
    public string? Description { get; init; }

    /// <summary>Keywords (XMP <c>dc:subject</c>)</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Star rating (XMP <c>xmp:Rating</c>, -1 to 5)</summary>
    public int? Rating { get; init; }

    /// <summary>
    /// Average colour as lowercase sRGB hex (<c>#rrggbb</c>), the theme's loading placeholder.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Gamut of the photo's content (<see cref="ImageGamut.Srgb"/> or <see cref="ImageGamut.P3"/>).
    /// </summary>
    public string? Gamut { get; init; }
}

