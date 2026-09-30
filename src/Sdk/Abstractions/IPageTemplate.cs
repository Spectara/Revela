namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Defines a page template for creating _index.revela files with frontmatter.
/// </summary>
/// <remarks>
/// Templates provide metadata and properties that drive the initialization process:
/// <list type="bullet">
/// <item><description><see cref="PageProperties"/> define frontmatter fields (title, description, etc.)</description></item>
/// <item><description>Each property is exposed as a CLI option with help text and examples</description></item>
/// </list>
/// </remarks>
public interface IPageTemplate
{
    /// <summary>
    /// Gets the template name used for CLI subcommand (e.g., "statistics" for "revela init page statistics").
    /// </summary>
    /// <remarks>
    /// Must be lowercase and URL-safe (alphanumeric, hyphens).
    /// Used as the lookup key for template discovery.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the display name shown in help text (e.g., "Photo Statistics").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the description shown in command help text.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the Scriban template name written to frontmatter (e.g., "statistics/overview").
    /// </summary>
    /// <remarks>
    /// Written as: <c>template = "statistics/overview"</c> in the generated _index.revela file.
    /// </remarks>
    string TemplateName { get; }

    /// <summary>
    /// Gets the properties that appear in page frontmatter (title, description, etc.).
    /// </summary>
    /// <remarks>
    /// These properties are exposed as CLI options in "revela init page {name}" command.
    /// Properties with <see cref="TemplateProperty.FrontmatterKey"/> are written to _index.revela.
    /// Properties with <c>FrontmatterKey = null</c> are CLI-only (like --path).
    /// </remarks>
    IReadOnlyList<TemplateProperty> PageProperties { get; }

    /// <summary>
    /// Gets the default body content (Markdown) to include after frontmatter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional starter content to help users get started.
    /// Return <c>null</c> or empty string for no default body.
    /// </para>
    /// <para>
    /// The content is written directly after the closing <c>+++</c> frontmatter marker.
    /// </para>
    /// </remarks>
    string? DefaultBody => null;
}
