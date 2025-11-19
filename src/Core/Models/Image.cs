namespace Spectara.Revela.Core.Models;

/// <summary>
/// Represents an image with its metadata and processing variants
/// </summary>
public sealed class Image
{
    public required string SourcePath { get; init; }
    public required string FileName { get; init; }
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

