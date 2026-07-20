using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Renders inline-gallery blocks through the active theme.
/// </summary>
internal sealed class GalleryBlockRenderer(ContentImageContext context) : HtmlObjectRenderer<GalleryBlock>
{
    private int bareGalleryCount;

    /// <inheritdoc />
    protected override void Write(HtmlRenderer renderer, GalleryBlock block)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(block);

        var galleryContext = context.GalleryBlocks
            ?? throw new InvalidOperationException("Inline gallery rendering requires a gallery block context.");
        var line = block.Line + 1;
        galleryContext.EnsureGalleryGrid(line);

        galleryContext.MarkInlineGallery();

        var images = block.FilterExpression is null
            ? ResolvePageImages(galleryContext, line)
            : ResolveFilteredImages(block.FilterExpression);

        if (images.Count == 0)
        {
            var filterDescription = block.FilterExpression is null
                ? "the page-local image set"
                : $"filter '{block.FilterExpression}'";
            galleryContext.ReportWarning(
                $"{galleryContext.SourcePath}:{line}: inline gallery {filterDescription} matched 0 photos.");
            return;
        }

        renderer.Write(galleryContext.RenderGalleryGrid(images, line));
    }

    private IReadOnlyList<Models.Image> ResolveFilteredImages(string filterExpression)
    {
        var resolve = context.ResolveGalleryImages
            ?? throw new InvalidOperationException("Inline gallery filtering requires a global image resolver.");
        return resolve(filterExpression);
    }

    private IReadOnlyList<Models.Image> ResolvePageImages(GalleryBlockContext galleryContext, int line)
    {
        bareGalleryCount++;
        if (bareGalleryCount > 1)
        {
            galleryContext.ReportWarning(
                $"{galleryContext.SourcePath}:{line}: multiple bare [[gallery]] blocks render the same page-local image set; use a filter to differentiate them.");
        }

        return galleryContext.PageImages;
    }
}
