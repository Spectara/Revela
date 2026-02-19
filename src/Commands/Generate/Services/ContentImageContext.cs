using Spectara.Revela.Commands.Generate.Models;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Context for resolving image references in Markdown body content.
/// </summary>
/// <remarks>
/// <para>
/// Provides the Markdig <see cref="ContentImageExtension"/> with all data needed
/// to transform <c>![alt](path)</c> into responsive <c>&lt;picture&gt;</c> elements.
/// </para>
/// <para>
/// Image lookup priority:
/// <list type="number">
/// <item>Gallery-local: <c>{GalleryPath}/{markdownPath}</c></item>
/// <item>Shared images: <c>_images/{markdownPath}</c></item>
/// <item>Exact match: <c>{markdownPath}</c> as-is</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="ImagesBySourcePath">
/// Lookup of all processed images by normalized source path (forward slashes).
/// Includes gallery images and shared <c>_images/</c> content.
/// </param>
/// <param name="GalleryPath">
/// Relative filesystem path of the current gallery (e.g., "docs/getting-started").
/// Empty string for root.
/// </param>
/// <param name="ImageBasePath">
/// Base URL path to the images directory (e.g., "../images/" or CDN URL).
/// </param>
/// <param name="ImageFormats">
/// Active image formats in priority order (e.g., ["avif", "webp", "jpg"]).
/// </param>
internal sealed record ContentImageContext(
    IReadOnlyDictionary<string, Image> ImagesBySourcePath,
    string GalleryPath,
    string ImageBasePath,
    IEnumerable<string> ImageFormats);
