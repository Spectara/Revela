using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Commands.Create.Templates;

/// <summary>
/// Page template for creating text-only pages without gallery.
/// </summary>
/// <remarks>
/// <para>
/// Creates a _index.revela file for pages like About, Contact, Imprint, etc.
/// Uses the "page" template which renders only the markdown body without image grid.
/// </para>
/// <para>
/// Usage: revela create page text source/about --title "About Me"
/// </para>
/// </remarks>
internal sealed class TextPageTemplate : IPageTemplate
{
    /// <inheritdoc />
    public string Name => "text";

    /// <inheritdoc />
    public string DisplayName => "Text Page";

    /// <inheritdoc />
    public string Description => "Create a text-only page (About, Contact, Imprint, etc.)";

    /// <inheritdoc />
    /// <remarks>
    /// Uses "page" template which renders body content without gallery grid.
    /// </remarks>
    public string TemplateName => "page";

    /// <inheritdoc />
    public string ConfigSectionName => "";

    /// <inheritdoc />
    public bool HasConfigCommand => false;

    /// <inheritdoc />
    public IReadOnlyList<TemplateProperty> PageProperties { get; } =
    [
        new()
        {
            Name = "title",
            Aliases = ["--title", "-t"],
            Type = typeof(string),
            DefaultValue = "Page",
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
            Description = "Page description (for SEO)",
            Required = false,
            FrontmatterKey = "description",
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
    public IReadOnlyList<TemplateProperty> ConfigProperties { get; } = [];

    /// <inheritdoc />
    public string DefaultBody => """
        Write your content here using **Markdown**.

        ## Example Heading

        - List item one
        - List item two

        *Edit this file to add your own content.*
        """;
}
