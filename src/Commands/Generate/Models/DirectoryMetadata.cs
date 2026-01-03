namespace Spectara.Revela.Commands.Generate.Models;

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
/// template: statistics/overview
/// data:
///   statistics: statistics.json
///   galleries: $galleries
/// ---
/// Optional body content in Markdown or Scriban.
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
    /// Gets the template type for rendering.
    /// </summary>
    /// <remarks>
    /// Specifies a custom template for rendering this page's content.
    /// The template is rendered to gallery.body and wrapped by Layout.revela.
    /// </remarks>
    public string? Template { get; init; }

    /// <summary>
    /// Gets the sort override for images in this gallery.
    /// </summary>
    /// <remarks>
    /// <para>Format: <c>field</c> or <c>field:direction</c></para>
    /// <para>Examples:</para>
    /// <list type="bullet">
    /// <item><c>dateTaken</c> - Sort by date (direction from global config)</item>
    /// <item><c>dateTaken:asc</c> - Sort by date, oldest first</item>
    /// <item><c>exif.raw.Rating:desc</c> - Sort by rating, highest first</item>
    /// </list>
    /// </remarks>
    public string? Sort { get; init; }

    /// <summary>
    /// Gets the data sources for template rendering.
    /// </summary>
    /// <remarks>
    /// <para>Maps variable names to data sources:</para>
    /// <list type="bullet">
    /// <item><c>statistics: statistics.json</c> - Load JSON file</item>
    /// <item><c>galleries: $galleries</c> - All galleries in the site</item>
    /// <item><c>images: $images</c> - Images in current folder</item>
    /// </list>
    /// <para>Legacy single-value syntax also supported: <c>data: statistics.json</c></para>
    /// </remarks>
    public IReadOnlyDictionary<string, string> DataSources { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the raw body content before any processing.
    /// </summary>
    /// <remarks>
    /// Contains the raw Markdown/Scriban content from the file.
    /// When Template is set, this should be processed as Scriban first.
    /// </remarks>
    public string? RawBody { get; init; }

    /// <summary>
    /// Gets the rendered HTML body content from the Markdown below the frontmatter.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Gets a value indicating whether any metadata was found.
    /// </summary>
    public bool HasMetadata => Title is not null || Slug is not null || Description is not null ||
                               Hidden || Body is not null || Template is not null || Sort is not null ||
                               DataSources.Count > 0;

    /// <summary>
    /// Gets an empty metadata instance.
    /// </summary>
    public static DirectoryMetadata Empty { get; } = new();
}
