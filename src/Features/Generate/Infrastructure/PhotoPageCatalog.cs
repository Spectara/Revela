using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Infrastructure;

/// <summary>
/// Builds the render-time photo-page catalog: one <see cref="PhotoPage"/> per unique
/// published source image, aggregated from every eligible gallery membership.
/// </summary>
/// <remarks>
/// <para>
/// A gallery is eligible when it is rendered (visible) and uses the effective default gallery
/// body (<see cref="Gallery.Template"/> is <c>null</c> or resolves to <c>body/gallery</c>).
/// Galleries with custom bodies (<c>body/page</c>, <c>statistics/overview</c>, the home body,
/// …) neither create photo pages nor become contexts.
/// </para>
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
    /// Builds photo pages from the reconstructed galleries. Galleries must already carry their
    /// final <see cref="Gallery.Images"/> order.
    /// </summary>
    /// <param name="galleries">All galleries in stable site/navigation order (root first).</param>
    /// <returns>One page per unique eligible source image, in first-occurrence order.</returns>
    public static IReadOnlyList<PhotoPage> Build(IReadOnlyList<Gallery> galleries)
    {
        var eligible = galleries.Where(IsEligible).ToList();

        // Preserve first-occurrence order while grouping every membership by source identity.
        var order = new List<string>();
        var groups = new Dictionary<string, List<Occurrence>>(StringComparer.OrdinalIgnoreCase);

        foreach (var gallery in eligible)
        {
            var images = gallery.Images;
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
                list.Add(new Occurrence(gallery, image, previous, next));
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
                    GallerySlug = occurrence.Gallery.Slug,
                    Label = GalleryLabel(occurrence.Gallery),
                    ContextId = ContextId(occurrence.Gallery.Slug),
                    IsPhysical = IsPhysical(occurrence.Gallery, occurrence.Image),
                    PreviousPhoto = occurrence.Previous,
                    NextPhoto = occurrence.Next
                })
                .ToList();

            var primary = contexts.FirstOrDefault(context => context.IsPhysical) ?? contexts[0];

            pages.Add(new PhotoPage
            {
                Image = identity,
                Slug = identity.Slug,
                Anchor = GalleryAnchor(identity.Slug),
                Title = PageTitle(identity),
                PrimaryContext = primary,
                Contexts = contexts
            });
        }

        return pages;
    }

    /// <summary>
    /// Whether a gallery is eligible to create photo pages and contexts: rendered and using
    /// the effective default gallery body.
    /// </summary>
    public static bool IsEligible(Gallery gallery) =>
        gallery.Template is null
        || gallery.Template.Equals("gallery", StringComparison.OrdinalIgnoreCase)
        || gallery.Template.Equals("body/gallery", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Stable HTML id token (without the <c>ctx-</c> prefix) for a gallery-context fragment.
    /// The site root maps to <c>"home"</c>; other galleries reuse their output slug with path
    /// separators replaced by hyphens.
    /// </summary>
    public static string ContextId(string gallerySlug) =>
        gallerySlug.Length == 0 ? "home" : gallerySlug.Replace('/', '-');

    /// <summary>
    /// Stable gallery-side anchor id (<c>photo-</c> prefix) for an image slug so <c>up</c>
    /// links land on the originating gallery occurrence.
    /// </summary>
    public static string GalleryAnchor(string imageSlug) =>
        "photo-" + imageSlug.Trim('/').Replace('/', '-');

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

    private static string NormalizeSourcePath(string path) => path.Replace('\\', '/').Trim('/');

    private readonly record struct Occurrence(Gallery Gallery, Image Image, Image? Previous, Image? Next);
}
