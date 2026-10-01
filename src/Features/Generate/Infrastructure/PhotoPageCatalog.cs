using System.Globalization;
using System.Text;

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
                    Anchor = occurrence.Membership.PhotoNumber is { } photoNumber
                        ? PhotoAnchor(occurrence.Image.Slug, photoNumber)
                        : Anchor(occurrence.Image.Slug, occurrence.Membership.GridNumber),
                    // A [[photo]] block is a page reference, never the photo's gallery home.
                    IsPhysical = occurrence.Membership.PhotoNumber is null &&
                        IsPhysical(occurrence.Membership.Gallery, occurrence.Image),
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
    /// The site root maps to <c>r</c>; other galleries use <c>g-</c> followed by the readable
    /// encoded slug (see <see cref="EncodeSlug"/>), e.g. <c>g-jahre_2018</c>.
    /// </summary>
    public static string BaseContextId(string gallerySlug)
    {
        var normalized = gallerySlug.Trim('/');
        return normalized.Length == 0 ? "r" : $"g-{EncodeSlug(normalized)}";
    }

    /// <summary>
    /// Stable context id for a base, filtered-grid (<c>.grid-n</c>) or photo-block
    /// (<c>.photo-n</c>) membership. Grids and photo blocks are numbered independently.
    /// The <c>.</c> separator never occurs in an encoded slug, so a gallery whose slug ends in
    /// <c>-grid-1</c> cannot collide with the first grid of another gallery.
    /// </summary>
    public static string ContextId(PhotoMembership membership)
    {
        var baseContextId = BaseContextId(membership.Gallery.Slug);
        if (membership.PhotoNumber is { } photoNumber)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{baseContextId}.photo-{photoNumber}");
        }

        return membership.GridNumber is null
            ? baseContextId
            : string.Create(CultureInfo.InvariantCulture, $"{baseContextId}.grid-{membership.GridNumber.Value}");
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

    /// <summary>
    /// Stable page-side anchor id for a <c>[[photo]]</c> block occurrence
    /// (<c>photo-{n}-photo-i-</c> prefix, distinct from gallery and grid anchors).
    /// </summary>
    public static string PhotoAnchor(string imageSlug, int photoNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"photo-{photoNumber}-photo-i-{EncodeSlug(imageSlug.Trim('/'))}");

    /// <summary>
    /// Readable, injective encoding of a canonical slug for HTML ids and URL fragments.
    /// </summary>
    /// <remarks>
    /// Slug characters <c>a-z</c>, <c>0-9</c> and <c>-</c> stay as they are, <c>/</c> becomes
    /// <c>_</c>, and every other UTF-16 code unit becomes <c>~</c> plus four hex digits. The
    /// result never contains <c>.</c>, which <see cref="ContextId"/> reserves as its separator,
    /// and all output characters are unreserved in URLs, so fragments need no percent-encoding.
    /// </remarks>
    private static string EncodeSlug(string slug)
    {
        var builder = new StringBuilder(slug.Length);
        foreach (var codeUnit in slug)
        {
            if (codeUnit is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
            {
                builder.Append(codeUnit);
            }
            else if (codeUnit == '/')
            {
                builder.Append('_');
            }
            else
            {
                builder.Append('~').Append(((int)codeUnit).ToString("x4", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

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
/// Frozen image order for one eligible base gallery, filtered inline-grid, or
/// <c>[[photo]]</c> block occurrence.
/// </summary>
/// <param name="Gallery">The page the occurrence is rendered on.</param>
/// <param name="Images">The frozen image order of this occurrence.</param>
/// <param name="GridNumber">The filtered inline-grid number, or <c>null</c>.</param>
/// <param name="ViewerMode">The effective viewer mode of this occurrence.</param>
/// <param name="PhotoNumber">
/// The <c>[[photo]]</c> block number (own namespace, independent of <paramref name="GridNumber"/>), or <c>null</c>.
/// </param>
internal sealed record PhotoMembership(
    Gallery Gallery,
    IReadOnlyList<Image> Images,
    int? GridNumber,
    PhotoViewerMode ViewerMode,
    int? PhotoNumber = null)
{
    /// <summary>
    /// Gets whether this is the page's base gallery membership (not a filtered grid or photo block).
    /// </summary>
    public bool IsBase => GridNumber is null && PhotoNumber is null;
}
