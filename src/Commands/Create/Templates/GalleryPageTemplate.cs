using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Commands.Create.Templates;

/// <summary>
/// Core page template for creating gallery pages.
/// </summary>
/// <remarks>
/// <para>
/// Creates a _index.revela file with frontmatter for gallery pages.
/// Supports all standard frontmatter fields: title, description, sort, hidden, and slug.
/// </para>
/// <para>
/// Usage: revela create page gallery source/vacation --title "Summer 2024"
/// </para>
/// <para>
/// Advanced: revela create page gallery source/best --title "Best Shots" --sort "exif.raw.Rating:desc"
/// </para>
/// </remarks>
public sealed class GalleryPageTemplate : IPageTemplate
{
    /// <inheritdoc />
    public string Name => "gallery";

    /// <inheritdoc />
    public string DisplayName => "Gallery Page";

    /// <inheritdoc />
    public string Description => "Create a gallery page with title, description, and sorting options";

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
        },
        new()
        {
            Name = "sort",
            Aliases = ["--sort", "-s"],
            Type = typeof(string),
            DefaultValue = null,
            Description = "Sort override (e.g., 'dateTaken:asc', 'exif.raw.Rating:desc')",
            Required = false,
            FrontmatterKey = "sort",
            ConfigKey = null
        },
        new()
        {
            Name = "hidden",
            Aliases = ["--hidden"],
            Type = typeof(bool),
            DefaultValue = false,
            Description = "Hide from navigation (page still accessible via URL)",
            Required = false,
            FrontmatterKey = "hidden",
            ConfigKey = null
        },
        new()
        {
            Name = "slug",
            Aliases = ["--slug"],
            Type = typeof(string),
            DefaultValue = null,
            Description = "Custom URL segment (overrides folder name)",
            Required = false,
            FrontmatterKey = "slug",
            ConfigKey = null
        }
    ];

    /// <inheritdoc />
    /// <remarks>
    /// Gallery pages don't require any plugin configuration.
    /// </remarks>
    public IReadOnlyList<TemplateProperty> ConfigProperties { get; } = [];

    /// <inheritdoc />
    /// <remarks>
    /// Optional introduction text shown above the image grid.
    /// </remarks>
    public string? DefaultBody => """
        Add an optional introduction here.

        This text appears above the image gallery.
        """;
}
