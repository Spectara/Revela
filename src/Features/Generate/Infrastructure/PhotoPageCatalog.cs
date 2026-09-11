using System.Globalization;

using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Features.Generate.Infrastructure;

/// <summary>
/// Builds the render-time photo-page catalog: one <see cref="PhotoPage"/> per unique
/// published source image, aggregated from every page-viewer membership.
/// </summary>
/// <remarks>
/// <para>
/// Occurrences are grouped by normalized <see cref="Image.SourcePath"/> using
/// ordinal-ignore-case comparison. Context order follows the gallery's final rendered image
/// order (after filtering, sorting, and limiting); filters are never re-evaluated here.
/// Previous/next are derived per membership without wraparound.
/// </para>
/// </remarks>
internal static class PhotoPageCatalog
{
    /// <summary>
    /// Builds page-viewer base memberships for callers without prepared rendering metadata.
    /// </summary>
    public static IReadOnlyList<PhotoPage> Build(IReadOnlyList<Gallery> galleries) =>
        Build(
            [.. galleries.Select(gallery =>
                new PhotoMembership(gallery, gallery.Images, null, PhotoViewerMode.Page))]);

    /// <summary>
    /// Builds photo pages from explicit gallery memberships with frozen image order.
    /// </summary>
    /// <param name="memberships">Page-viewer memberships in stable document order.</param>
    /// <returns>One page per unique source image, in first-occurrence order.</returns>
    public static IReadOnlyList<PhotoPage> Build(IReadOnlyList<PhotoMembership> memberships)
    {
        // Preserve first-occurrence order while grouping every membership by source identity.
        var order = new List<string>();
        var groups = new Dictionary<string, List<Occurrence>>(StringComparer.OrdinalIgnoreCase);

        foreach (var membership in memberships)
        {
            var images = membership.Images;
            for (var index = 0; index < images.Count; index++)
            {
                var image = images[index];
                var key = NormalizeSourcePath(image.SourcePath);

                if (!groups.TryGetValue(key, out var list))
                {
                    list = [];
                    groups[key] = list;
                    order.Add(key);
                }

                var previous = index > 0 ? images[index - 1] : null;
                var next = index < images.Count - 1 ? images[index + 1] : null;
                list.Add(new Occurrence(membership, image, previous, next));
            }
        }

        var pages = new List<PhotoPage>(order.Count);

        foreach (var key in order)
        {
            var occurrences = groups[key];
            var identity = occurrences[0].Image;

            var contexts = occurrences
                .Select(occurrence => new PhotoContext
                {
                    Route = occurrence.Membership.Gallery.Slug,
                    Label = GalleryLabel(occurrence.Membership.Gallery),
                    ContextId = ContextId(occurrence.Membership),
                    Anchor = Anchor(occurrence.Image.Slug, occurrence.Membership.GridNumber),
                    IsPhysical = IsPhysical(occurrence.Membership.Gallery, occurrence.Image),
                    PreviousPhoto = occurrence.Previous,
                    NextPhoto = occurrence.Next
                })
                .ToList();

            var primary = contexts.FirstOrDefault(context => context.IsPhysical) ?? contexts[0];

            pages.Add(new PhotoPage
            {
                Image = identity,
                Slug = identity.Slug,
                Title = PageTitle(identity),
                PrimaryContext = primary,
                Contexts = contexts
            });
        }

        return pages;
    }

    /// <summary>
    /// Stable HTML id token (without the <c>ctx-</c> prefix) for a gallery-context fragment.
    /// The site root maps to <c>r</c>; other galleries use <c>g-</c> followed by
    /// fixed-width hexadecimal UTF-16 code units, preserving distinct canonical slugs.
    /// </summary>
    public static string BaseContextId(string gallerySlug)
    {
        var normalized = gallerySlug.Trim('/');
        return normalized.Length == 0 ? "r" : $"g-{EncodeSlug(normalized)}";
    }

    /// <summary>
    /// Stable context id for a base or filtered membership.
    /// </summary>
    public static string ContextId(PhotoMembership membership)
    {
        var baseContextId = BaseContextId(membership.Gallery.Slug);
        return membership.GridNumber is null
            ? baseContextId
            : string.Create(CultureInfo.InvariantCulture, $"{baseContextId}-grid-{membership.GridNumber.Value}");
    }

    /// <summary>
    /// Stable gallery-side anchor id (<c>photo-i-</c> prefix) for an image slug so <c>up</c>
    /// links land on the originating gallery occurrence.
    /// </summary>
    public static string Anchor(string imageSlug, int? gridNumber)
    {
        var encodedImageSlug = EncodeSlug(imageSlug.Trim('/'));
        return gridNumber is null
            ? $"photo-i-{encodedImageSlug}"
            : string.Create(CultureInfo.InvariantCulture, $"grid-{gridNumber.Value}-photo-i-{encodedImageSlug}");
    }

    private static string EncodeSlug(string slug) =>
        string.Concat(slug.Select(codeUnit => ((int)codeUnit).ToString("x4", CultureInfo.InvariantCulture)));

    private static string PageTitle(Image image) =>
        !string.IsNullOrWhiteSpace(image.Title) ? image.Title : image.FileName;

    private static string GalleryLabel(Gallery gallery) =>
        !string.IsNullOrWhiteSpace(gallery.Title) ? gallery.Title : gallery.Name;

    private static bool IsPhysical(Gallery gallery, Image image)
    {
        var directory = NormalizeSourcePath(image.SourcePath);
        var lastSlash = directory.LastIndexOf('/');
        directory = lastSlash < 0 ? string.Empty : directory[..lastSlash];

        return string.Equals(directory, NormalizeSourcePath(gallery.Path), StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeSourcePath(string path) => path.Replace('\\', '/').Trim('/');

    private readonly record struct Occurrence(
        PhotoMembership Membership,
        Image Image,
        Image? Previous,
        Image? Next);
}

/// <summary>
/// Frozen image order for one eligible base gallery or filtered inline-grid occurrence.
/// </summary>
internal sealed record PhotoMembership(
    Gallery Gallery,
    IReadOnlyList<Image> Images,
    int? GridNumber,
    PhotoViewerMode ViewerMode);
