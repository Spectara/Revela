using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Renders inline-gallery blocks through the active theme.
/// </summary>
internal sealed class GalleryBlockRenderer(ContentImageContext context) : HtmlObjectRenderer<GalleryBlock>
{
    /// <inheritdoc />
    protected override void Write(HtmlRenderer renderer, GalleryBlock block)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(block);

        var galleryContext = context.GalleryBlocks
            ?? throw new InvalidOperationException("Inline gallery rendering requires a gallery block context.");
        var line = block.Line + 1;
        var blockId = new GalleryBlockId(block.Line, block.Column);
        if (!galleryContext.PreparedBlocks.Blocks.TryGetValue(blockId, out var preparedBlock))
        {
            throw new InvalidOperationException(
                $"{galleryContext.SourcePath}:{line}: inline gallery was not prepared before rendering.");
        }

        galleryContext.EnsureGalleryGrid(line);

        if (preparedBlock.IsDuplicateBare)
        {
            galleryContext.ReportWarning(
                $"{galleryContext.SourcePath}:{line}: multiple bare [[gallery]] blocks render the same page-local image set; use a filter to differentiate them.");
        }

        if (preparedBlock.Images.Count == 0)
        {
            var filterDescription = preparedBlock.FilterExpression is null
                ? "the page-local image set"
                : $"filter '{preparedBlock.FilterExpression}'";
            galleryContext.ReportWarning(
                $"{galleryContext.SourcePath}:{line}: inline gallery {filterDescription} matched 0 photos.");
            return;
        }

        renderer.Write(galleryContext.RenderGalleryGrid(preparedBlock, line));
    }
}
