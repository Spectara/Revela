using Markdig;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Registers standalone inline-gallery (<c>[[gallery]]</c>) and photo (<c>[[photo]]</c>) blocks
/// with a Markdig pipeline.
/// </summary>
internal sealed class GalleryBlockExtension : IMarkdownExtension
{
    private readonly string sourcePath;
    private readonly ContentImageContext? context;

    public GalleryBlockExtension(string sourcePath, ContentImageContext? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        this.sourcePath = sourcePath;
        this.context = context;
    }

    /// <inheritdoc />
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        if (!pipeline.BlockParsers.Any(parser => parser is GalleryBlockParser))
        {
            pipeline.BlockParsers.InsertBefore<ParagraphBlockParser>(new GalleryBlockParser(sourcePath));
        }

        if (!pipeline.BlockParsers.Any(parser => parser is PhotoBlockParser))
        {
            pipeline.BlockParsers.InsertBefore<ParagraphBlockParser>(new PhotoBlockParser(sourcePath));
        }

        if (context is not null && !pipeline.InlineParsers.Any(parser => parser is GalleryTokenWarningInlineParser))
        {
            pipeline.InlineParsers.InsertBefore<LinkInlineParser>(new GalleryTokenWarningInlineParser(context));
        }
    }

    /// <inheritdoc />
    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        if (context is null || renderer is not HtmlRenderer htmlRenderer)
        {
            return;
        }

        if (!htmlRenderer.ObjectRenderers.Any(objectRenderer => objectRenderer is GalleryBlockRenderer))
        {
            htmlRenderer.ObjectRenderers.Add(new GalleryBlockRenderer(context));
        }

        if (!htmlRenderer.ObjectRenderers.Any(objectRenderer => objectRenderer is PhotoBlockRenderer))
        {
            htmlRenderer.ObjectRenderers.Add(new PhotoBlockRenderer(context));
        }
    }
}
