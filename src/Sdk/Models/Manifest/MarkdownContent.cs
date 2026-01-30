namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Content item representing a markdown file.
/// </summary>
/// <remarks>
/// The markdown body is loaded at render time from the source file,
/// not stored in the manifest to keep it lean.
/// </remarks>
public sealed record MarkdownContent : GalleryContent
{
    /// <summary>
    /// Hash for change detection.
    /// </summary>
    /// <remarks>
    /// Computed from filename, size, and last modified date.
    /// Used to detect when markdown needs to be re-rendered.
    /// </remarks>
    public string Hash { get; init; } = string.Empty;
}
