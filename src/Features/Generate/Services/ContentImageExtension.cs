using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Markdig extension that transforms image references in Markdown body content
/// into responsive <c>&lt;picture&gt;</c> elements with AVIF/WebP/JPG srcset.
/// </summary>
/// <remarks>
/// <para>
/// Intercepts Markdown image syntax <c>![alt](path)</c> and resolves the path
/// against processed images from the site manifest. When a match is found,
/// generates a full <c>&lt;picture&gt;</c> element with format sources and responsive srcset.
/// </para>
/// <para>
/// Paths resolve like every other image reference (see <see cref="ImagePathResolver"/>).
/// </para>
/// <para>
/// External URLs (http/https) and unresolved paths fall through to standard
/// Markdig <c>&lt;img&gt;</c> rendering.
/// </para>
/// </remarks>
internal sealed class ContentImageExtension(ContentImageContext context) : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        // No AST modifications needed
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer)
        {
            // Replace the default LinkInline renderer with our custom one
            var defaultRenderer = htmlRenderer.ObjectRenderers.FindExact<LinkInlineRenderer>();
            if (defaultRenderer is not null)
            {
                htmlRenderer.ObjectRenderers.Remove(defaultRenderer);
            }

            htmlRenderer.ObjectRenderers.Add(new ContentImageRenderer(context, defaultRenderer));
        }
    }
}

/// <summary>
/// Custom renderer for <see cref="LinkInline"/> that generates <c>&lt;picture&gt;</c>
/// elements for image references matching processed site images.
/// </summary>
internal sealed class ContentImageRenderer : HtmlObjectRenderer<LinkInline>
{
    private readonly ContentImageContext context;
    private readonly LinkInlineRenderer? defaultRenderer;

    public ContentImageRenderer(ContentImageContext context, LinkInlineRenderer? defaultRenderer)
    {
        this.context = context;
        this.defaultRenderer = defaultRenderer ?? new LinkInlineRenderer();
    }

    protected override void Write(HtmlRenderer renderer, LinkInline link)
    {
        // Only handle images, not regular links
        if (!link.IsImage)
        {
            defaultRenderer?.Write(renderer, link);
            return;
        }

        var url = link.Url;

        // Skip external URLs — let default renderer handle them
        if (string.IsNullOrEmpty(url) || IsExternalUrl(url))
        {
            defaultRenderer?.Write(renderer, link);
            return;
        }

        // Try to resolve the image from processed site images
        var image = ImagePathResolver.Resolve(url, context.GalleryPath, context.ImagesBySourcePath);
        if (image is null || image.Sizes.Count == 0)
        {
            // Not a processed image — fall through to default <img>
            defaultRenderer?.Write(renderer, link);
            return;
        }

        // Extract alt text and optional CSS classes from generic attributes
        var altText = GetAltText(link);
        var extraClasses = link.TryGetAttributes()?.Classes;

        // Render via theme template (Partials/ContentImage.revela)
        var html = context.RenderContentImage(image, altText, extraClasses);
        renderer.Write(html);
    }


    private static string GetAltText(LinkInline link)
    {
        var sb = new StringBuilder();
        foreach (var child in link)
        {
            if (child is LiteralInline literal)
            {
                sb.Append(literal.Content);
            }
        }

        return sb.ToString();
    }

    private static bool IsExternalUrl(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("//", StringComparison.Ordinal);
}

