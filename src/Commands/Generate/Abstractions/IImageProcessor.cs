using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Commands.Generate.Abstractions;

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
public interface IImageProcessor
{
    /// <summary>
    /// Process a single image: resize, convert formats, extract EXIF
    /// </summary>
    /// <param name="inputPath">Path to the source image</param>
    /// <param name="options">Processing options (sizes, formats, quality)</param>
    /// <param name="onVariantSaved">Callback invoked after each variant is saved (skipped: true if from cache)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed image with variants and EXIF data</returns>
    Task<Image> ProcessImageAsync(
        string inputPath,
        ImageProcessingOptions options,
        Action<bool>? onVariantSaved = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read image metadata without processing (fast operation).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads only the image header - does NOT decode full image (unless placeholderConfig is set).
    /// Used during scan phase for:
    /// - Width/Height extraction
    /// - EXIF data extraction
    /// - Hash calculation
    /// </para>
    /// <para>
    /// When <paramref name="placeholderConfig"/> is provided with Strategy != None,
    /// the placeholder is generated during scan and included in the metadata.
    /// This requires loading the full image but ensures placeholders are available
    /// when pages are generated.
    /// </para>
    /// </remarks>
    /// <param name="inputPath">Path to the source image</param>
    /// <param name="placeholderConfig">Optional placeholder configuration (null = no placeholder)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Image metadata (dimensions, EXIF, file info, optional placeholder)</returns>
    Task<ImageMetadata> ReadMetadataAsync(
        string inputPath,
        PlaceholderConfig? placeholderConfig = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Image metadata extracted without full processing.
/// </summary>
public sealed class ImageMetadata
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

    /// <summary>
    /// Pre-generated placeholder for lazy loading (CSS-only LQIP hash).
    /// </summary>
    /// <remarks>
    /// Generated during scan when PlaceholderConfig is provided.
    /// Contains a 20-bit integer as string (e.g., "-721311").
    /// </remarks>
    public string? Placeholder { get; init; }
}
