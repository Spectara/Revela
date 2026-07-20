using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// A first-class photo page: one canonical page per unique published source image,
/// aggregated from every eligible gallery membership.
/// </summary>
/// <remarks>
/// <para>
/// Identity is the normalized <see cref="Image.SourcePath"/>. Every occurrence of the same
/// source image across eligible galleries is folded into a single page; the individual
/// memberships become navigation <see cref="Contexts"/>.
/// </para>
/// <para>
/// This aggregate is private to the Generate feature and produced at render time by
/// <see cref="Infrastructure.PhotoPageCatalog"/>.
/// </para>
/// </remarks>
[RevelaTemplateModel]
internal sealed class PhotoPage
{
    /// <summary>The identity image rendered on this page.</summary>
    public required Image Image { get; init; }

    /// <summary>
    /// Canonical route slug (e.g. <c>"landscapes/ocean-sunset"</c>); the page is written to
    /// <c>output/photo/{Slug}/index.html</c> and served at <c>/photo/{Slug}/</c>.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Stable gallery-side anchor id (e.g. <c>"photo-landscapes-ocean-sunset"</c>) that the
    /// <c>up</c> link targets so navigation returns to the originating gallery occurrence.
    /// </summary>
    public required string Anchor { get; init; }

    /// <summary>
    /// Deterministic document title: the image title when available, otherwise a filename/slug
    /// fallback. Guarantees a unique, non-empty <c>&lt;title&gt;</c> per page.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The context selected for direct navigation (no fragment) and as the fallback for an
    /// unknown or stale fragment. Prefers the physical gallery, otherwise the first eligible
    /// gallery in stable site order.
    /// </summary>
    public required PhotoContext PrimaryContext { get; init; }

    /// <summary>
    /// Every eligible gallery membership in stable site order.
    /// </summary>
    public required IReadOnlyList<PhotoContext> Contexts { get; init; }
}
