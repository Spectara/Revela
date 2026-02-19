namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Source image before processing
/// </summary>
/// <remarks>
/// Represents an image file discovered during content scanning,
/// before any processing (resizing, format conversion) has occurred.
/// </remarks>
internal sealed class SourceImage
{
    /// <summary>
    /// Full filesystem path to source image
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Path relative to source directory
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Image filename without path
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    /// Last modification timestamp
    /// </summary>
    public required DateTime LastModified { get; init; }

    /// <summary>
    /// Gallery path this image belongs to
    /// </summary>
    public required string Gallery { get; init; }
}
