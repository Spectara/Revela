namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Metadata parsed from _index.md files in gallery directories.
/// </summary>
/// <remarks>
/// Supports YAML frontmatter with the following fields:
/// <code>
/// ---
/// title: My Custom Title
/// slug: custom-slug
/// description: A description for SEO
/// hidden: true
/// ---
/// Optional body content in Markdown.
/// </code>
/// </remarks>
public sealed class DirectoryMetadata
{
    /// <summary>
    /// Gets the display title for the directory.
    /// Overrides the auto-generated title from the folder name.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the URL slug for the directory.
    /// Only affects the last segment of the URL path.
    /// </summary>
    public string? Slug { get; init; }

    /// <summary>
    /// Gets the description for SEO and display purposes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether this directory should be hidden from navigation.
    /// The page is still generated and accessible via direct URL.
    /// </summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// Gets the rendered HTML body content from the Markdown below the frontmatter.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Gets a value indicating whether any metadata was found.
    /// </summary>
    public bool HasMetadata => Title is not null || Slug is not null || Description is not null || Hidden || Body is not null;

    /// <summary>
    /// Gets an empty metadata instance.
    /// </summary>
    public static DirectoryMetadata Empty { get; } = new();
}
