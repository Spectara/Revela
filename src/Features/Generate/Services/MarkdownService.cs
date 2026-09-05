using System.Collections.Frozen;
using Markdig;
using Markdig.Syntax;
using Spectara.Revela.Features.Generate.Filtering;
using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Service for converting Markdown to HTML using Markdig.
/// </summary>
/// <remarks>
/// <para>
/// Provides Markdown rendering with common extensions enabled:
/// </para>
/// <list type="bullet">
/// <item>AutoLinks - Automatic URL detection</item>
/// <item>AutoIdentifiers - Header IDs for anchors</item>
/// <item>Tables - GitHub-flavored tables</item>
/// <item>TaskLists - Checkbox lists</item>
/// </list>
/// <para>
/// When a <see cref="ContentImageContext"/> is provided, image references
/// (<c>![alt](path)</c>) are automatically transformed into responsive
/// <c>&lt;picture&gt;</c> elements with AVIF/WebP/JPG srcset.
/// </para>
/// </remarks>
internal interface IMarkdownService
{
    /// <summary>
    /// Parses and resolves inline-gallery blocks before page catalog construction.
    /// </summary>
    PreparedGalleryBlocks PrepareGalleryBlocks(
        string markdown,
        string sourcePath,
        IReadOnlyList<Image> pageImages,
        Func<string, IReadOnlyList<Image>> resolveGalleryImages);

    /// <summary>
    /// Converts Markdown text to HTML.
    /// </summary>
    /// <param name="markdown">The Markdown text to convert.</param>
    /// <returns>HTML representation of the Markdown.</returns>
    string ToHtml(string markdown);

    /// <summary>
    /// Converts Markdown text to HTML with content image resolution.
    /// </summary>
    /// <remarks>
    /// Image references (<c>![alt](path)</c>) matching processed site images
    /// are rendered as responsive <c>&lt;picture&gt;</c> elements.
    /// Unresolved images and external URLs use standard <c>&lt;img&gt;</c> tags.
    /// </remarks>
    /// <param name="markdown">The Markdown text to convert.</param>
    /// <param name="imageContext">Context for resolving image references to processed images.</param>
    /// <returns>HTML representation of the Markdown with responsive images.</returns>
    string ToHtml(string markdown, ContentImageContext imageContext);
}

/// <summary>
/// Markdig-based Markdown to HTML converter.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trust model:</b> Revela treats <c>_index.revela</c> bodies as trusted author content
/// and renders raw HTML verbatim. This matches Jekyll, Eleventy, MkDocs, Astro, and Zola — the
/// majority of single-author static site generators.
/// </para>
/// <para>
/// We deliberately do NOT call <c>.DisableHtml()</c> on the pipeline, even though it is available.
/// Markdig's own documentation makes the trade-off explicit:
/// </para>
/// <para>
/// <i>"Markdig is a Markdown processor, not an HTML sanitizer. Disabling HTML parsing reduces risk
/// from raw HTML input, but it does not make rendering untrusted Markdown to HTML 'safe' by itself.
/// If you accept user-provided Markdown, sanitize the generated HTML and consider filtering/rewriting
/// link and image URLs."</i>
/// — <see href="https://xoofx.github.io/markdig/docs/usage/#configuration-options"/>
/// </para>
/// <para>
/// In other words: <c>.DisableHtml()</c> alone is security theater. Real protection requires a
/// downstream HTML sanitizer (e.g. Ganss.Xss) plus URL-scheme filtering for <c>javascript:</c>,
/// <c>data:</c>, etc. Revela's threat model (single photographer, own content) does not justify
/// that complexity. If a multi-user / untrusted-contributor scenario emerges, see
/// <c>docs/security-model.md</c> for the upgrade path.
/// </para>
/// </remarks>
internal sealed class MarkdownService : IMarkdownService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseAutoIdentifiers()
        .UsePipeTables()
        .UseTaskLists()
        .UseGenericAttributes()
        // Raw HTML intentionally allowed — see class XML doc above.
        .Build();

    /// <inheritdoc />
    public PreparedGalleryBlocks PrepareGalleryBlocks(
        string markdown,
        string sourcePath,
        IReadOnlyList<Image> pageImages,
        Func<string, IReadOnlyList<Image>> resolveGalleryImages)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(pageImages);
        ArgumentNullException.ThrowIfNull(resolveGalleryImages);

        var pipeline = CreatePipelineBuilder()
            .Use(new GalleryBlockExtension(sourcePath))
            .UseGenericAttributes()
            .Build();
        var document = Markdown.Parse(markdown, pipeline);
        var blocks = new Dictionary<GalleryBlockId, PreparedGalleryBlock>();
        var bareBlockCount = 0;
        var filteredBlockCount = 0;

        foreach (var block in document.Descendants<GalleryBlock>())
        {
            var isDuplicateBare = false;
            IReadOnlyList<Image> images;
            if (block.FilterExpression is null)
            {
                bareBlockCount++;
                isDuplicateBare = bareBlockCount > 1;
                images = [.. pageImages];
            }
            else
            {
                filteredBlockCount++;
                try
                {
                    images = [.. resolveGalleryImages(block.FilterExpression)];
                }
                catch (FilterParseException ex)
                {
                    throw new GalleryBlockParseException(
                        sourcePath,
                        block.Line + 1,
                        block.FilterExpression,
                        ex);
                }
            }

            blocks.Add(
                new GalleryBlockId(block.Line, block.Column),
                new PreparedGalleryBlock(
                    images,
                    isDuplicateBare,
                    block.FilterExpression,
                    block.FilterExpression is null ? null : filteredBlockCount,
                    block.FilterExpression is null ? bareBlockCount : null));
        }

        return new PreparedGalleryBlocks(blocks.ToFrozenDictionary());
    }

    /// <inheritdoc/>
    public string ToHtml(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Markdown.ToHtml(markdown, Pipeline);
    }

    /// <inheritdoc/>
    public string ToHtml(string markdown, ContentImageContext imageContext)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(imageContext);

        var pipelineBuilder = CreatePipelineBuilder()
            .Use(new ContentImageExtension(imageContext));

        if (imageContext.GalleryBlocks is not null)
        {
            pipelineBuilder.Use(new GalleryBlockExtension(imageContext.GalleryBlocks.SourcePath, imageContext));
        }

        var pipeline = pipelineBuilder
            .UseGenericAttributes()
            .Build();

        return Markdown.ToHtml(markdown, pipeline);
    }

    private static MarkdownPipelineBuilder CreatePipelineBuilder() => new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseAutoIdentifiers()
        .UsePipeTables()
        .UseTaskLists();
}

