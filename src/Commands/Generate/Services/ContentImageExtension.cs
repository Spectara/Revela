using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Commands.Generate.Services;

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

        // Generate <picture> element
        WritePictureElement(renderer, image, altText, extraClasses);
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

    /// <summary>
    /// Generates a responsive <c>&lt;picture&gt;</c> element with format sources and srcset.
    /// </summary>
    private void WritePictureElement(HtmlRenderer renderer, Image image, string altText, List<string>? extraClasses = null)
    {
        var basePath = context.ImageBasePath;
        var formats = context.ImageFormats;
        var isLandscape = image.Width >= image.Height;
        var largestSize = image.Sizes[^1];
        var escapedAlt = EscapeHtmlAttribute(altText);

        renderer.Write("<picture class=\"content-image");
        if (extraClasses is { Count: > 0 })
        {
            foreach (var cls in extraClasses)
            {
                renderer.Write(" ");
                renderer.Write(cls);
            }
        }

        renderer.Write("\"");
        if (image.Placeholder is not null)
        {
            renderer.Write(" style=\"--lqip:");
            renderer.Write(image.Placeholder);
            renderer.Write("\"");
        }

        renderer.WriteLine(">");

        // <source> per format with responsive srcset
        foreach (var format in formats)
        {
            renderer.Write("  <source type=\"");
            renderer.Write(GetMimeType(format));
            renderer.Write("\" srcset=\"");
            WriteSrcset(renderer, image, basePath, format, isLandscape);
            renderer.WriteLine("\">");
        }

        // <img> fallback (largest JPG size)
        var fallbackFormat = "jpg";
        var fallbackSize = Math.Min(largestSize, 1280);
        // Pick the closest available size for the fallback
        var actualFallback = image.Sizes.LastOrDefault(s => s <= fallbackSize);
        if (actualFallback == 0)
        {
            actualFallback = image.Sizes[0];
        }

        renderer.Write("  <img src=\"");
        renderer.Write(basePath);
        renderer.Write(image.Url);
        renderer.Write("/");
        renderer.Write(actualFallback.ToString(CultureInfo.InvariantCulture));
        renderer.Write(".");
        renderer.Write(fallbackFormat);
        renderer.Write("\" alt=\"");
        renderer.Write(escapedAlt);
        renderer.Write("\" width=\"");
        renderer.Write(image.Width.ToString(CultureInfo.InvariantCulture));
        renderer.Write("\" height=\"");
        renderer.Write(image.Height.ToString(CultureInfo.InvariantCulture));
        renderer.WriteLine("\" loading=\"lazy\" decoding=\"async\">");

        renderer.WriteLine("</picture>");
    }

    /// <summary>
    /// Writes srcset attribute value with all available sizes for a format.
    /// </summary>
    private static void WriteSrcset(HtmlRenderer renderer, Image image, string basePath, string format, bool isLandscape)
    {
        for (var i = 0; i < image.Sizes.Count; i++)
        {
            var size = image.Sizes[i];
            var actualWidth = isLandscape
                ? size
                : (int)Math.Floor((double)size * image.Width / image.Height);

            if (i > 0)
            {
                renderer.Write(", ");
            }

            renderer.Write(basePath);
            renderer.Write(image.Url);
            renderer.Write("/");
            renderer.Write(size.ToString(CultureInfo.InvariantCulture));
            renderer.Write(".");
            renderer.Write(format);
            renderer.Write(" ");
            renderer.Write(actualWidth.ToString(CultureInfo.InvariantCulture));
            renderer.Write("w");
        }
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

    private static string GetMimeType(string format) => format switch
    {
        "avif" => "image/avif",
        "webp" => "image/webp",
        "jpg" => "image/jpeg",
        "jpeg" => "image/jpeg",
        "png" => "image/png",
        "gif" => "image/gif",
        _ => $"image/{format}"
    };

    private static string EscapeHtmlAttribute(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
             .Replace("\"", "&quot;", StringComparison.Ordinal)
             .Replace("<", "&lt;", StringComparison.Ordinal)
             .Replace(">", "&gt;", StringComparison.Ordinal);
}
