using System.Globalization;
using System.Text;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Categorises a slug conflict discovered during scanning.
/// </summary>
internal enum SlugConflictKind
{
    /// <summary>Two or more distinct gallery folders resolve to the same non-empty slug.</summary>
    GalleryCollision,

    /// <summary>A non-root gallery folder normalizes to an empty slug (would collide with the site root).</summary>
    GalleryEmpty,

    /// <summary>Two or more distinct image files resolve to the same output path.</summary>
    ImageCollision,

    /// <summary>An image file normalizes to an empty output path.</summary>
    ImageEmpty,

    /// <summary>Two or more distinct source images resolve to the same <c>/photo/</c> page route.</summary>
    PhotoCollision,

    /// <summary>A gallery, static, or generated route collides with the reserved <c>/photo/</c> namespace.</summary>
    PhotoRouteCollision
}

/// <summary>
/// Describes a single slug/output-path conflict found while scanning content.
/// </summary>
/// <remarks>
/// Slug normalization (<see cref="Infrastructure.UrlBuilder.ToSlug"/> /
/// <see cref="Infrastructure.UrlBuilder.ToImageSlug"/>) is not injective: distinct source
/// folders or files can normalize to the same output path, or to an empty one. Each instance
/// records the offending normalized slug together with every source path that produced it, so
/// the scan can abort with an actionable message instead of silently overwriting output.
/// </remarks>
internal sealed record SlugConflict(
    SlugConflictKind Kind,
    string Slug,
    IReadOnlyList<string> Sources)
{
    /// <summary>
    /// Renders this conflict as a human-readable block: the offending slug, every conflicting
    /// source path, and a concrete rename suggestion.
    /// </summary>
    public string Describe()
    {
        var builder = new StringBuilder();

        switch (Kind)
        {
            case SlugConflictKind.GalleryCollision:
                builder.Append(CultureInfo.InvariantCulture, $"Gallery slug '{Slug}' is produced by multiple sources:");
                AppendSources(builder);
                builder.Append("Rename one of these folders so each gallery gets a unique URL.");
                break;

            case SlugConflictKind.GalleryEmpty:
                builder.Append("These gallery folders normalize to an empty slug and would collide with the site root ('/'):");
                AppendSources(builder);
                builder.Append("Rename them so each gallery produces a unique, non-empty URL slug.");
                break;

            case SlugConflictKind.ImageCollision:
                builder.Append(CultureInfo.InvariantCulture, $"Image output path '{Slug}' is produced by multiple sources:");
                AppendSources(builder);
                builder.Append("Rename one of these files or folders so each image has a unique output path.");
                break;

            case SlugConflictKind.ImageEmpty:
                builder.Append("These image files normalize to an empty output path:");
                AppendSources(builder);
                builder.Append("Rename them so each image produces a non-empty output path.");
                break;

            case SlugConflictKind.PhotoCollision:
                builder.Append(CultureInfo.InvariantCulture, $"Photo page route '/{Slug}/' is produced by multiple source images:");
                AppendSources(builder);
                builder.Append("Rename one of these files so each published photo gets a unique canonical URL. " +
                    "Revela never auto-suffixes photo routes.");
                break;

            case SlugConflictKind.PhotoRouteCollision:
                builder.Append(CultureInfo.InvariantCulture, $"Route '/{Slug}/' collides with the reserved '/photo/' output namespace:");
                AppendSources(builder);
                builder.Append("Rename the gallery/static route so it does not start with 'photo/'.");
                break;

            default:
                throw new InvalidOperationException($"Unknown slug conflict kind: {Kind}");
        }

        return builder.ToString();
    }

    private void AppendSources(StringBuilder builder)
    {
        foreach (var source in Sources)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{Environment.NewLine}  - {source}");
        }

        builder.Append(Environment.NewLine);
    }
}
