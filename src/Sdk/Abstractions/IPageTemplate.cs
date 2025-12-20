namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Defines a page template for creating _index.revela files with frontmatter and optional plugin configuration.
/// </summary>
/// <remarks>
/// Templates provide metadata and properties that drive the initialization process:
/// <list type="bullet">
/// <item><description><see cref="PageProperties"/> define frontmatter fields (title, description, etc.)</description></item>
/// <item><description><see cref="ConfigProperties"/> define plugin configuration options (maxEntries, etc.)</description></item>
/// <item><description>Both property types are exposed as CLI options with help text and examples</description></item>
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
    /// Gets the configuration section name for plugin settings (e.g., "Spectara.Revela.Plugin.Statistics").
    /// </summary>
    /// <remarks>
    /// Used to auto-detect the plugin JSON filename: <c>{ConfigSectionName}.json</c>
    /// Also used as the root key in the JSON structure.
    /// </remarks>
    string ConfigSectionName { get; }

    /// <summary>
    /// Gets a value indicating whether a corresponding 'config {name}' command exists.
    /// </summary>
    /// <remarks>
    /// When true, 'init {name}' will show a hint to use 'config {name}' for interactive configuration.
    /// Default should be false for templates that only have init commands.
    /// </remarks>
    bool HasConfigCommand { get; }

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
    /// Gets the properties that appear in plugin configuration JSON.
    /// </summary>
    /// <remarks>
    /// These properties are exposed as CLI options in "revela init config {name}" command.
    /// Properties are written to plugins/{ConfigSectionName}.json using <see cref="TemplateProperty.ConfigKey"/>.
    /// Supports dot notation for nested objects (e.g., "Deploy.Host" → {"Deploy": {"Host": "..."}}).
    /// </remarks>
    IReadOnlyList<TemplateProperty> ConfigProperties { get; }
}
