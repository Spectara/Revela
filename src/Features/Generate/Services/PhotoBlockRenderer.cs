using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Renders <c>[[photo]]</c> blocks through the page-local photo callback.
/// </summary>
internal sealed class PhotoBlockRenderer(ContentImageContext context) : HtmlObjectRenderer<PhotoBlock>
{
    /// <inheritdoc />
    protected override void Write(HtmlRenderer renderer, PhotoBlock block)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(block);

        var galleryContext = context.GalleryBlocks
            ?? throw new InvalidOperationException("Photo block rendering requires a gallery block context.");
        var line = block.Line + 1;
        if (!galleryContext.PreparedBlocks.Photos.TryGetValue(new GalleryBlockId(block.Line, block.Column), out var preparedPhoto))
        {
            throw new InvalidOperationException(
                $"{galleryContext.SourcePath}:{line}: photo block was not prepared before rendering.");
        }

        if (preparedPhoto.Image is null || preparedPhoto.Image.Sizes.Count == 0)
        {
            galleryContext.ReportWarning(
                $"{galleryContext.SourcePath}:{line}: photo '{preparedPhoto.ImagePath}' matched no processed image " +
                "(looked in the page folder, _images/ and the source folder).");
            return;
        }

        if (galleryContext.RenderPhoto is { } renderPhoto)
        {
            renderer.Write(renderPhoto(preparedPhoto, line));
        }
    }
}
