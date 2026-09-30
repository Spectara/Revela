using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Features.Generate.Templates;

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
internal sealed class GalleryPageTemplate : IPageTemplate
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
            FrontmatterKey = "title"
        },
        new()
        {
            Name = "description",
            Aliases = ["--description", "-d"],
            Type = typeof(string),
            DefaultValue = "",
            Description = "Page description",
            Required = false,
            FrontmatterKey = "description"
        },
        new()
        {
            Name = "sort",
            Aliases = ["--sort", "-s"],
            Type = typeof(string),
            DefaultValue = null,
            Description = "Sort override (e.g., 'dateTaken:asc', 'exif.raw.Rating:desc')",
            Required = false,
            FrontmatterKey = "sort"
        },
        new()
        {
            Name = "hidden",
            Aliases = ["--hidden"],
            Type = typeof(bool),
            DefaultValue = false,
            Description = "Hide from navigation (page still accessible via URL)",
            Required = false,
            FrontmatterKey = "hidden"
        },
        new()
        {
            Name = "slug",
            Aliases = ["--slug"],
            Type = typeof(string),
            DefaultValue = null,
            Description = "Custom URL segment (overrides folder name)",
            Required = false,
            FrontmatterKey = "slug"
        }
    ];

    /// <inheritdoc />
    /// <remarks>
    /// Optional introduction text shown above the image grid.
    /// </remarks>
    public string? DefaultBody => """
        Add an optional introduction here.

        This text appears above the image gallery.
        """;
}

