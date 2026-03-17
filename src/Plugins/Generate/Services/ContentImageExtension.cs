using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;
using Spectara.Revela.Plugins.Generate.Models;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Plugins.Generate.Services;

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
/// Image resolution priority:
/// <list type="number">
/// <item>Gallery-local: <c>{GalleryPath}/{path}</c></item>
/// <item>Shared images: <c>_images/{path}</c></item>
/// <item>Exact match: path as-is</item>
/// </list>
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
        var image = ResolveImage(url);
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

    /// <summary>
    /// Resolves an image path from Markdown against the processed image lookup.
    /// </summary>
    private Image? ResolveImage(string markdownPath)
    {
        // Normalize to forward slashes
        var normalizedPath = markdownPath.Replace('\\', '/');

        // 1. Gallery-local: {GalleryPath}/{path}
        if (!string.IsNullOrEmpty(context.GalleryPath))
        {
            var localPath = $"{context.GalleryPath}/{normalizedPath}";
            if (context.ImagesBySourcePath.TryGetValue(localPath, out var localImage))
            {
                return localImage;
            }
        }

        // 2. Shared images: _images/{path}
        var sharedPath = $"{ProjectPaths.SharedImages}/{normalizedPath}";
        if (context.ImagesBySourcePath.TryGetValue(sharedPath, out var sharedImage))
        {
            return sharedImage;
        }

        // 3. Exact match (e.g., user wrote _images/screenshots/awesome.jpg explicitly)
        if (context.ImagesBySourcePath.TryGetValue(normalizedPath, out var exactImage))
        {
            return exactImage;
        }

        return null;
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
