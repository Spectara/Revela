using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Resolves an author-written image path (Markdown image, <c>cover</c>, <c>[[photo]]</c>,
/// <c>find_image</c>) to a processed image.
/// </summary>
/// <remarks>
/// Lookup order:
/// <list type="number">
/// <item>Page folder: <c>{pagePath}/{path}</c></item>
/// <item>Shared images: <c>_images/{path}</c></item>
/// <item>Exact source path: <c>{path}</c> as written (e.g. <c>_images/screenshots/a.jpg</c>)</item>
/// </list>
/// </remarks>
internal static class ImagePathResolver
{
    /// <summary>
    /// Resolves <paramref name="imagePath"/> for the page at <paramref name="pagePath"/>.
    /// </summary>
    /// <param name="imagePath">Path as written by the author; backslashes are accepted.</param>
    /// <param name="pagePath">Source-relative folder of the page; empty for the site root.</param>
    /// <param name="imagesBySourcePath">All processed images keyed by source path with forward slashes.</param>
    /// <returns>The image, or <c>null</c> when no processed image matches.</returns>
    public static Image? Resolve(
        string imagePath,
        string pagePath,
        IReadOnlyDictionary<string, Image> imagesBySourcePath)
    {
        var normalizedPath = imagePath.Replace('\\', '/');

        if (!string.IsNullOrEmpty(pagePath) &&
            imagesBySourcePath.TryGetValue($"{pagePath.Replace('\\', '/')}/{normalizedPath}", out var pageImage))
        {
            return pageImage;
        }

        if (imagesBySourcePath.TryGetValue($"{ProjectPaths.SharedImages}/{normalizedPath}", out var sharedImage))
        {
            return sharedImage;
        }

        return imagesBySourcePath.GetValueOrDefault(normalizedPath);
    }
}
