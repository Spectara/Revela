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
internal sealed record PreparedGalleryBlocks(
    IReadOnlyDictionary<GalleryBlockId, PreparedGalleryBlock> Blocks)
{
    public static PreparedGalleryBlocks Empty { get; } = new(
        new Dictionary<GalleryBlockId, PreparedGalleryBlock>().ToFrozenDictionary());

    public int Count => Blocks.Count;
}

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
/// Provides page-local rendering callbacks for inline-gallery blocks.
/// </summary>
internal sealed record GalleryBlockContext(
    string SourcePath,
    PreparedGalleryBlocks PreparedBlocks,
    Action<int> EnsureGalleryGrid,
    Func<PreparedGalleryBlock, int, string> RenderGalleryGrid,
    Action<string> ReportWarning);
