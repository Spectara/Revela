using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Infrastructure;

/// <summary>
/// Detects empty or colliding normalized output slugs across scanned galleries and images.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UrlBuilder.ToSlug"/> and <see cref="UrlBuilder.ToImageSlug"/> are not injective:
/// distinct source folders/files can normalize to the same output slug (for example
/// <c>01 Events</c> and <c>02 Events</c> → <c>events</c>, or <c>Café</c> and <c>Cafe</c> →
/// <c>cafe</c>), and names consisting only of removed characters (such as <c>!!!</c>) normalize
/// to an empty slug. Because gallery pages are written to <c>output/{gallery.Slug}</c> and image
/// variants reuse the same normalized segments, two sources can silently overwrite each other.
/// </para>
/// <para>
/// This validator compares the <em>full</em> normalized output slug, so it does not flag the #51
/// case where identical filenames live under genuinely distinct gallery slugs — those produce
/// distinct image slugs and never collide.
/// </para>
/// </remarks>
internal static class SlugValidator
{
    /// <summary>
    /// Finds every slug conflict across the given galleries and images. Aggregates all conflicts
    /// (collect-all) so callers can surface them together rather than one at a time.
    /// </summary>
    public static IReadOnlyList<SlugConflict> FindConflicts(
        IReadOnlyList<Gallery> galleries,
        IReadOnlyList<SourceImage> images)
    {
        var conflicts = new List<SlugConflict>();

        CollectGalleryConflicts(galleries, conflicts);
        CollectImageConflicts(images, conflicts);

        return conflicts;
    }

    private static void CollectGalleryConflicts(IReadOnlyList<Gallery> galleries, List<SlugConflict> conflicts)
    {
        // Gallery slugs carry a trailing '/' (from UrlBuilder.BuildPath); the site root is "".
        var groups = galleries
            .GroupBy(gallery => gallery.Slug.TrimEnd('/'), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var sources = group
                .Select(gallery => gallery.Path)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            if (group.Key.Length == 0)
            {
                // Only the site root (empty Path) may legitimately produce an empty slug.
                var nonRoot = sources
                    .Where(path => !string.IsNullOrEmpty(path))
                    .ToList();

                if (nonRoot.Count > 0)
                {
                    conflicts.Add(new SlugConflict(SlugConflictKind.GalleryEmpty, string.Empty, nonRoot));
                }

                continue;
            }

            if (sources.Count > 1)
            {
                conflicts.Add(new SlugConflict(SlugConflictKind.GalleryCollision, group.Key, sources));
            }
        }
    }

    private static void CollectImageConflicts(IReadOnlyList<SourceImage> images, List<SlugConflict> conflicts)
    {
        var groups = images
            .GroupBy(image => UrlBuilder.ToImageSlug(image.RelativePath), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var sources = group
                .Select(image => image.RelativePath.Replace('\\', '/'))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            if (group.Key.Length == 0)
            {
                conflicts.Add(new SlugConflict(SlugConflictKind.ImageEmpty, string.Empty, sources));
                continue;
            }

            if (sources.Count > 1)
            {
                conflicts.Add(new SlugConflict(SlugConflictKind.ImageCollision, group.Key, sources));
            }
        }
    }

    /// <summary>
    /// Finds every photo-page route conflict before any output is written: two distinct source
    /// images resolving to the same <c>/photo/{slug}/</c> page, and gallery/static routes that
    /// collide with the reserved <c>/photo/</c> namespace. Never auto-suffixes.
    /// </summary>
    public static IReadOnlyList<SlugConflict> FindPhotoConflicts(
        IReadOnlyList<PhotoPage> photoPages,
        IReadOnlyList<Gallery> galleries)
    {
        var conflicts = new List<SlugConflict>();

        // 1. Distinct source images that would overwrite each other under /photo/.
        var slugGroups = photoPages
            .GroupBy(page => page.Slug, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in slugGroups)
        {
            var sources = group
                .Select(page => page.Image.SourcePath.Replace('\\', '/'))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            if (sources.Count > 1)
            {
                conflicts.Add(new SlugConflict(SlugConflictKind.PhotoCollision, $"photo/{group.Key}", sources));
            }
        }

        // 2. Gallery routes colliding with the reserved /photo/ namespace or a specific page.
        var photoRoutes = new HashSet<string>(
            photoPages.Select(page => $"photo/{page.Slug}"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var gallery in galleries)
        {
            var route = gallery.Slug.TrimEnd('/');
            if (route.Length == 0)
            {
                continue;
            }

            var collidesWithNamespace =
                route.Equals("photo", StringComparison.OrdinalIgnoreCase) ||
                route.StartsWith("photo/", StringComparison.OrdinalIgnoreCase) ||
                photoRoutes.Contains(route);

            if (collidesWithNamespace)
            {
                conflicts.Add(new SlugConflict(SlugConflictKind.PhotoRouteCollision, route, [gallery.Path]));
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Builds a single, actionable scan-error message that aggregates every conflict.
    /// </summary>
    public static string FormatScanError(IReadOnlyList<SlugConflict> conflicts)
    {
        var header = conflicts.Count == 1
            ? "Scan aborted: 1 slug conflict would overwrite generated output."
            : $"Scan aborted: {conflicts.Count} slug conflicts would overwrite generated output.";

        return FormatConflicts(conflicts, header);
    }

    /// <summary>
    /// Builds a single, actionable render-error message for photo-route conflicts detected
    /// before any output is written.
    /// </summary>
    public static string FormatPhotoRouteError(IReadOnlyList<SlugConflict> conflicts)
    {
        var header = conflicts.Count == 1
            ? "Render aborted: 1 photo-page route conflict would overwrite generated output."
            : $"Render aborted: {conflicts.Count} photo-page route conflicts would overwrite generated output.";

        return FormatConflicts(conflicts, header);
    }

    private static string FormatConflicts(IReadOnlyList<SlugConflict> conflicts, string header)
    {
        var separator = Environment.NewLine + Environment.NewLine;
        return header + separator + string.Join(separator, conflicts.Select(conflict => conflict.Describe()));
    }
}
