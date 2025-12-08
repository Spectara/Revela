using Spectara.Revela.Commands.Generate.Models;

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
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed image with variants and EXIF data</returns>
    Task<Image> ProcessImageAsync(
        string inputPath,
        ImageProcessingOptions options,
        CancellationToken cancellationToken = default);
}
