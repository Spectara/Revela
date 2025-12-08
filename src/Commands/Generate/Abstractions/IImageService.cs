using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Results;

namespace Spectara.Revela.Commands.Generate.Abstractions;

/// <summary>
/// Service for image processing (resize, convert, EXIF extraction).
/// </summary>
/// <remarks>
/// <para>
/// Processes images from the manifest:
/// </para>
/// <list type="bullet">
///   <item><description>Generate responsive image variants (multiple sizes)</description></item>
///   <item><description>Convert to modern formats (WebP, AVIF)</description></item>
///   <item><description>Extract and cache EXIF metadata</description></item>
///   <item><description>Skip unchanged images (hash-based caching)</description></item>
/// </list>
/// </remarks>
public interface IImageService
{
    /// <summary>
    /// Process images from manifest (resize, convert, extract EXIF).
    /// </summary>
    /// <param name="options">Image processing options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result with statistics.</returns>
    Task<ImageResult> ProcessAsync(
        ProcessImagesOptions options,
        IProgress<ImageProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a single image and generate all size variants.
    /// </summary>
    /// <param name="inputPath">Path to source image.</param>
    /// <param name="options">Processing options (sizes, formats, quality).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processed image with all variants.</returns>
    Task<Image> ProcessImageAsync(
        string inputPath,
        ImageProcessingOptions options,
        CancellationToken cancellationToken = default);
}
