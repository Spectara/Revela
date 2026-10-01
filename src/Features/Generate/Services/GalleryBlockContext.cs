using System.Collections.Frozen;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Stable source identity for an inline-gallery block.
/// </summary>
internal readonly record struct GalleryBlockId(int Line, int Column);

/// <summary>
/// Frozen result of preparing an inline-gallery block.
/// </summary>
internal sealed record PreparedGalleryBlock(
    IReadOnlyList<Image> Images,
    bool IsDuplicateBare,
    string? FilterExpression,
    int? GridNumber,
    int? BareRenderOrdinal);

/// <summary>
/// Prepared inline-gallery blocks keyed by their source identity.
/// </summary>
/// <remarks>
/// <see cref="Count"/> counts only <c>[[gallery]]</c> blocks: <c>[[photo]]</c> blocks never
/// suppress the trailing gallery grid.
/// </remarks>
internal sealed record PreparedGalleryBlocks(
    IReadOnlyDictionary<GalleryBlockId, PreparedGalleryBlock> Blocks)
{
    public static PreparedGalleryBlocks Empty { get; } = new(
        new Dictionary<GalleryBlockId, PreparedGalleryBlock>().ToFrozenDictionary());

    /// <summary>
    /// Gets the prepared <c>[[photo]]</c> blocks keyed by their source identity.
    /// </summary>
    public IReadOnlyDictionary<GalleryBlockId, PreparedPhotoBlock> Photos { get; init; } =
        new Dictionary<GalleryBlockId, PreparedPhotoBlock>().ToFrozenDictionary();

    public int Count => Blocks.Count;
}

/// <summary>
/// Frozen result of preparing a <c>[[photo: path]]</c> block.
/// </summary>
/// <param name="Image">The resolved image, or <c>null</c> when the path matches no processed image.</param>
/// <param name="ImagePath">The path as written in the token.</param>
/// <param name="UsesPageContext">
/// <c>true</c> when the photo page returns to this page; <c>false</c> for <c>| gallery</c>.
/// </param>
/// <param name="PhotoNumber">
/// 1-based document-order number among the page's photo blocks. Photo blocks use their own
/// numbering namespace so adding one never shifts <see cref="PreparedGalleryBlock.GridNumber"/>.
/// </param>
internal sealed record PreparedPhotoBlock(
    Image? Image,
    string ImagePath,
    bool UsesPageContext,
    int PhotoNumber);

/// <summary>
/// An image occurrence prepared for inline-gallery rendering.
/// </summary>
[RevelaTemplateModel]
internal sealed record GalleryImageOccurrence(
    Image Image,
    string ViewerMode,
    string ContextId,
    string ContextLabel,
    string OccurrenceId,
    string? PreviousOccurrenceId,
    string? NextOccurrenceId);

/// <summary>
/// Provides page-local rendering callbacks for inline-gallery and photo blocks.
/// </summary>
/// <param name="SourcePath">The <c>_index.revela</c> path used in warnings and errors.</param>
/// <param name="PreparedBlocks">Blocks prepared before photo-page catalog construction.</param>
/// <param name="EnsureGalleryGrid">Fails with a source location when the grid partial is missing.</param>
/// <param name="RenderGalleryGrid">Renders a prepared <c>[[gallery]]</c> block.</param>
/// <param name="ReportWarning">Reports a source-located warning.</param>
/// <param name="RenderPhoto">
/// Renders a prepared, resolved <c>[[photo]]</c> block; <c>null</c> renders nothing.
/// </param>
internal sealed record GalleryBlockContext(
    string SourcePath,
    PreparedGalleryBlocks PreparedBlocks,
    Action<int> EnsureGalleryGrid,
    Func<PreparedGalleryBlock, int, string> RenderGalleryGrid,
    Action<string> ReportWarning,
    Func<PreparedPhotoBlock, int, string>? RenderPhoto = null);
