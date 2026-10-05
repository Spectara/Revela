using Spectara.Revela.Features.Generate.Filtering;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Resolves render images from the filterable manifest image pool.
/// </summary>
internal static class GalleryImageResolver
{
    /// <summary>
    /// Applies the shared filter grammar before converting matching manifest entries to render images.
    /// </summary>
    /// <param name="imageContentsBySourcePath">Manifest images by source path.</param>
    /// <param name="filterExpression">Filter query, e.g. <c>all | sort dateTaken desc</c>.</param>
    /// <param name="wideGamut">Whether P3 photos are published in Display P3 (<c>generate.images.wideGamut</c>).</param>
    /// <param name="pageSort">The page's sort, if any.</param>
    /// <param name="globalSort">The project's image sort, if any.</param>
    public static IReadOnlyList<Image> Resolve(
        IReadOnlyDictionary<string, ImageContent> imageContentsBySourcePath,
        string filterExpression,
        bool wideGamut,
        string? pageSort = null,
        ImageSortConfig? globalSort = null)
    {
        ArgumentNullException.ThrowIfNull(imageContentsBySourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var sourcePaths = new Dictionary<ImageContent, string>(ReferenceEqualityComparer.Instance);
        foreach (var (sourcePath, imageContent) in imageContentsBySourcePath)
        {
            sourcePaths.Add(imageContent, sourcePath);
        }

        return [.. FilterService
            .ApplyQuery(imageContentsBySourcePath.Values, filterExpression, pageSort, globalSort)
            .Select(imageContent => Image.FromManifestEntry(sourcePaths[imageContent], imageContent, wideGamut))];
    }
}
