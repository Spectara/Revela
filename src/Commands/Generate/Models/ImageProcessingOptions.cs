namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Image processing options
/// </summary>
public sealed class ImageProcessingOptions
{
    /// <summary>
    /// Output formats with quality (1-100).
    /// Key = format (avif, webp, jpg), Value = quality
    /// </summary>
    public required IReadOnlyDictionary<string, int> Formats { get; init; }

    /// <summary>
    /// Sizes to generate in pixels (e.g., [640, 1280, 1920])
    /// </summary>
    public required IReadOnlyList<int> Sizes { get; init; }

    /// <summary>
    /// Output directory for processed images
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Optional cache directory for incremental builds
    /// </summary>
    public string? CacheDirectory { get; init; }

    /// <summary>
    /// Which dimension to use for resizing images.
    /// </summary>
    /// <remarks>
    /// - "longest": Size applies to the longest side (default, best for justified galleries)
    /// - "width": Size applies to width (all images same width)
    /// - "height": Size applies to height (all images same height)
    /// </remarks>
    public string ResizeMode { get; init; } = "longest";

    /// <summary>
    /// Specific variants to generate (incremental mode).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, only these specific size/format combinations will be generated.
    /// This enables incremental generation after config changes (e.g., adding a new size).
    /// </para>
    /// <para>
    /// When null, all combinations of <see cref="Sizes"/> × <see cref="Formats"/> are generated.
    /// </para>
    /// </remarks>
    public IReadOnlyList<(int Size, string Format)>? VariantsToGenerate { get; init; }
}
