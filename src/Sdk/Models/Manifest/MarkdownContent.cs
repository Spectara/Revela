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
    // No additional properties needed.
    // Filename, FileSize, and Hash are inherited from GalleryContent.
    // The rendered HTML body is loaded from the source file during rendering.
}
