namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Represents a gallery containing images
/// </summary>
public sealed class Gallery
{
    /// <summary>
    /// Original filesystem path relative to source directory
    /// </summary>
    /// <remarks>
    /// Used for finding images and matching source files.
    /// Example: "01 Events/Fireworks"
    /// </remarks>
    public required string Path { get; init; }

    /// <summary>
    /// URL-safe path for output directory and links
    /// </summary>
    /// <remarks>
    /// Normalized version of Path with slugified segments.
    /// Example: "events/fireworks/"
    /// </remarks>
    public required string Slug { get; init; }

    /// <summary>
    /// Gallery display name derived from folder name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional custom title from front matter.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Optional description from front matter.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Rendered HTML body content from _index.revela (below frontmatter).
    /// </summary>
    /// <remarks>
    /// Loaded at render time, not stored in manifest to keep file size small.
    /// </remarks>
    public string? Body { get; set; }

    /// <summary>
    /// Custom body template name from frontmatter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specifies which body template to use inside the layout.
    /// Default is "body/gallery" if not specified.
    /// </para>
    /// <example>
    /// template = "body/page" - Simple text page without gallery
    /// template = "statistics/overview" - Statistics plugin template
    /// </example>
    /// </remarks>
    public string? Template { get; set; }

    /// <summary>
    /// Data sources for template rendering from frontmatter.
    /// </summary>
    /// <remarks>
    /// Maps variable names to data source paths (e.g., { "statistics": "statistics.json" }).
    /// Plugins can query manifest for galleries with specific data sources.
    /// </remarks>
    public IReadOnlyDictionary<string, string> DataSources { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Optional cover image filename.
    /// </summary>
    public string? Cover { get; init; }

    /// <summary>
    /// Sort override for images in this gallery.
    /// </summary>
    /// <remarks>
    /// <para>Format: <c>field</c> or <c>field:direction</c></para>
    /// <para>Overrides the global sort configuration from project.json.</para>
    /// </remarks>
    public string? Sort { get; init; }

    /// <summary>
    /// Filter expression to select images from the entire site.
    /// </summary>
    /// <remarks>
    /// <para>When set, images are selected by filtering all site images,</para>
    /// <para>instead of using only images in this gallery's directory.</para>
    /// </remarks>
    public string? Filter { get; init; }

    /// <summary>
    /// Gallery date for sorting (from front matter or first image EXIF).
    /// </summary>
    public DateTime? Date { get; init; }

    /// <summary>
    /// Whether this gallery is featured on the home page.
    /// </summary>
    public bool Featured { get; init; }

    /// <summary>
    /// Manual sort weight (lower values appear first).
    /// </summary>
    public int Weight { get; init; }

    /// <summary>
    /// Images contained in this gallery.
    /// </summary>
    public IReadOnlyList<Image> Images { get; init; } = [];

    /// <summary>
    /// Nested sub-galleries.
    /// </summary>
    public IReadOnlyList<Gallery> SubGalleries { get; init; } = [];
}
