using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// A single eligible gallery membership of a photo, used only to drive navigation
/// (previous / up / next) on the photo page. A context never changes photo identity,
/// canonical URL, content, or SEO metadata.
/// </summary>
/// <remarks>
/// Contexts are render-specific navigation state. They are intentionally kept out of the
/// context-free <see cref="Image"/> identity model (<see cref="Image.SourcePath"/>).
/// </remarks>
[RevelaTemplateModel]
internal sealed class PhotoContext
{
    /// <summary>
    /// Output slug of the gallery this membership belongs to (e.g. <c>"landscapes/"</c>);
    /// empty string for the site root gallery.
    /// </summary>
    public required string GallerySlug { get; init; }

    /// <summary>
    /// Human-readable gallery label (title or folder name) shown in the memberships list.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Stable, valid HTML id token for this context, without the <c>ctx-</c> prefix
    /// (e.g. <c>"landscapes-"</c>, or <c>"home"</c> for the root gallery). The gallery-side
    /// thumbnail link carries <c>#ctx-{ContextId}</c> and the photo page exposes a matching
    /// <c>id="ctx-{ContextId}"</c> block selected via <c>:target</c>.
    /// </summary>
    public required string ContextId { get; init; }

    /// <summary>
    /// Whether this context is the physical gallery whose folder contains the source file
    /// (as opposed to a filter gallery that pulls a shared <c>_images/</c> source).
    /// </summary>
    public bool IsPhysical { get; init; }

    /// <summary>
    /// Previous photo in this gallery's final image order, or <c>null</c> at the first
    /// position (no wraparound).
    /// </summary>
    public Image? PreviousPhoto { get; init; }

    /// <summary>
    /// Next photo in this gallery's final image order, or <c>null</c> at the last position
    /// (no wraparound).
    /// </summary>
    public Image? NextPhoto { get; init; }
}
