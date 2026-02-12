using Markdig;

namespace Spectara.Revela.Commands.Generate.Services;

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
public interface IMarkdownService
{
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
public sealed class MarkdownService : IMarkdownService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseAutoIdentifiers()
        .UsePipeTables()
        .UseTaskLists()
        .UseGenericAttributes()
        .Build();

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

        var pipeline = new MarkdownPipelineBuilder()
            .UseAutoLinks()
            .UseAutoIdentifiers()
            .UsePipeTables()
            .UseTaskLists()
            .Use(new ContentImageExtension(imageContext))
            .UseGenericAttributes()
            .Build();

        return Markdown.ToHtml(markdown, pipeline);
    }
}
