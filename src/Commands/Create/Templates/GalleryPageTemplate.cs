using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Commands.Create.Templates;

/// <summary>
/// Core page template for creating gallery pages.
/// </summary>
/// <remarks>
/// <para>
/// Creates a basic _index.revela file with title and description frontmatter.
/// This template works with any theme's default gallery rendering.
/// </para>
/// <para>
/// Usage: revela create page gallery source/vacation --title "Summer 2024"
/// </para>
/// </remarks>
public sealed class GalleryPageTemplate : IPageTemplate
{
    /// <inheritdoc />
    public string Name => "gallery";

    /// <inheritdoc />
    public string DisplayName => "Gallery Page";

    /// <inheritdoc />
    public string Description => "Create a gallery page with title and description";

    /// <inheritdoc />
    /// <remarks>
    /// Empty string means use the theme's default template (usually body/gallery).
    /// </remarks>
    public string TemplateName => "";

    /// <inheritdoc />
    /// <remarks>
    /// Empty string means no plugin configuration is needed.
    /// </remarks>
    public string ConfigSectionName => "";

    /// <inheritdoc />
    /// <remarks>
    /// Gallery pages don't have a dedicated config command.
    /// </remarks>
    public bool HasConfigCommand => false;

    /// <inheritdoc />
    public IReadOnlyList<TemplateProperty> PageProperties { get; } =
    [
        new()
        {
            Name = "title",
            Aliases = ["--title", "-t"],
            Type = typeof(string),
            DefaultValue = "Gallery",
            Description = "Page title",
            Required = false,
            FrontmatterKey = "title",
            ConfigKey = null
        },
        new()
        {
            Name = "description",
            Aliases = ["--description", "-d"],
            Type = typeof(string),
            DefaultValue = "",
            Description = "Page description",
            Required = false,
            FrontmatterKey = "description",
            ConfigKey = null
        }
    ];

    /// <inheritdoc />
    /// <remarks>
    /// Gallery pages don't require any plugin configuration.
    /// </remarks>
    public IReadOnlyList<TemplateProperty> ConfigProperties { get; } = [];
}
