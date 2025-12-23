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
/// </remarks>
public interface IMarkdownService
{
    /// <summary>
    /// Converts Markdown text to HTML.
    /// </summary>
    /// <param name="markdown">The Markdown text to convert.</param>
    /// <returns>HTML representation of the Markdown.</returns>
    string ToHtml(string markdown);
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
        .Build();

    /// <inheritdoc/>
    public string ToHtml(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Markdown.ToHtml(markdown, Pipeline);
    }
}
