using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Unified manifest entry representing a node in the site tree.
/// </summary>
/// <remarks>
/// <para>
/// Each node can be either a gallery (has slug) or a branch (no slug, only children).
/// The root node represents the home page and contains the entire site structure.
/// </para>
/// <para>
/// Images are stored directly in each node, eliminating the need for a separate
/// images dictionary and path-based lookups.
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Required for JSON deserialization")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Template engine requires string paths")]
public sealed class ManifestEntry
{
    /// <summary>
    /// Display text for navigation and title.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>
    /// URL-safe slug for output. Null for branch nodes (non-gallery containers).
    /// </summary>
    /// <example>"events/fireworks/" for galleries, null for sections like "Events"</example>
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    /// <summary>
    /// Original filesystem path relative to source directory.
    /// </summary>
    /// <example>"01 Events\Fireworks"</example>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Optional description from front matter.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Optional cover image filename.
    /// </summary>
    [JsonPropertyName("cover")]
    public string? Cover { get; init; }

    /// <summary>
    /// Gallery date for sorting (from front matter or first image EXIF).
    /// </summary>
    [JsonPropertyName("date")]
    public DateTime? Date { get; init; }

    /// <summary>
    /// Whether this gallery is featured on the home page.
    /// </summary>
    [JsonPropertyName("featured")]
    public bool Featured { get; init; }

    /// <summary>
    /// Whether this item is hidden from navigation.
    /// </summary>
    [JsonPropertyName("hidden")]
    public bool Hidden { get; init; }

    /// <summary>
    /// Optional custom template for rendering (e.g., "statistics/overview").
    /// </summary>
    [JsonPropertyName("template")]
    public string? Template { get; init; }

    /// <summary>
    /// Data sources for template rendering from frontmatter.
    /// Maps variable names to data source paths (e.g., { "statistics": "statistics.json" }).
    /// </summary>
    [JsonPropertyName("dataSources")]
    public Dictionary<string, string> DataSources { get; init; } = [];

    /// <summary>
    /// Content items (images and markdown files) contained in this node.
    /// Sorted alphabetically by filename for predictable ordering.
    /// </summary>
    [JsonPropertyName("content")]
    public List<GalleryContent> Content { get; init; } = [];

    /// <summary>
    /// Child nodes (sub-galleries or branch sections).
    /// </summary>
    [JsonPropertyName("children")]
    public List<ManifestEntry> Children { get; init; } = [];
}
