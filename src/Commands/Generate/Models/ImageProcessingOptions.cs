namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Image processing options
/// </summary>
public sealed class ImageProcessingOptions
{
    /// <summary>
    /// JPEG quality (0-100)
    /// </summary>
    public required int Quality { get; init; }

    /// <summary>
    /// Output formats to generate (e.g., ["jpg", "webp"])
    /// </summary>
    public required IReadOnlyList<string> Formats { get; init; }

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
}
