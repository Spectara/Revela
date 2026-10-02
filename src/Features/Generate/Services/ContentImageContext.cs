using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Context for resolving image references in Markdown body content.
/// </summary>
/// <remarks>
/// Provides the Markdig <see cref="ContentImageExtension"/> with all data needed
/// to transform <c>![alt](path)</c> into responsive <c>&lt;picture&gt;</c> elements.
/// Paths resolve like every other image reference (see <see cref="ImagePathResolver"/>).
/// </remarks>
/// <param name="ImagesBySourcePath">
/// Lookup of all processed images by normalized source path (forward slashes).
/// Includes gallery images and shared <c>_images/</c> content.
/// </param>
/// <param name="GalleryPath">
/// Relative filesystem path of the current gallery (e.g., "docs/getting-started").
/// Empty string for root.
/// </param>
/// <param name="RenderContentImage">
/// Delegate to render a content image via theme template (Partials/ContentImage.revela).
/// Parameters: (Image image, string alt, List&lt;string&gt;? classes) → HTML string.
/// </param>
/// <param name="GalleryBlocks">
/// Optional page-local context for inline-gallery (<c>[[gallery]]</c>) parsing and rendering.
/// </param>
internal sealed record ContentImageContext(
    IReadOnlyDictionary<string, Image> ImagesBySourcePath,
    string GalleryPath,
    Func<Image, string, List<string>?, string> RenderContentImage,
    GalleryBlockContext? GalleryBlocks = null);
