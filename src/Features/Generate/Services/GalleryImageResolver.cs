using Spectara.Revela.Features.Generate.Filtering;
using Spectara.Revela.Features.Generate.Models;
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
    public static IReadOnlyList<Image> Resolve(
        IReadOnlyDictionary<string, ImageContent> imageContentsBySourcePath,
        string filterExpression)
    {
        ArgumentNullException.ThrowIfNull(imageContentsBySourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var sourcePaths = new Dictionary<ImageContent, string>(ReferenceEqualityComparer.Instance);
        foreach (var (sourcePath, imageContent) in imageContentsBySourcePath)
        {
            sourcePaths.Add(imageContent, sourcePath);
        }

        return [.. FilterService
            .ApplyQuery(imageContentsBySourcePath.Values, filterExpression)
            .Select(imageContent => Image.FromManifestEntry(sourcePaths[imageContent], imageContent))];
    }
}
