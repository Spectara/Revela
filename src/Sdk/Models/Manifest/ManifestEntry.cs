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
/// <para>
/// <b>Immutable:</b> all properties are <c>init</c>-only and collections are
/// <see cref="IReadOnlyList{T}"/> / <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
/// To update a node, use the <c>with</c> expression to produce a modified copy:
/// <code>
/// var updated = node with { Content = [.. node.Content, newImage] };
/// </code>
/// </para>
/// <para>
/// <b>Cross-platform:</b> path-like properties (<see cref="Slug"/>, <see cref="Path"/>,
/// <see cref="Cover"/>) are <see cref="RelativePath"/> values, which automatically
/// normalize Windows backslashes to forward slashes for consistent comparison
/// across operating systems.
/// </para>
/// </remarks>
public sealed record ManifestEntry
{
    /// <summary>
    /// Display text for navigation and title.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>
    /// URL-safe slug for output. Null for branch nodes (non-gallery containers).
    /// </summary>
    /// <example><c>"events/fireworks/"</c> for galleries, null for sections like "Events"</example>
    [JsonPropertyName("slug")]
    public RelativePath? Slug { get; init; }

    /// <summary>
    /// Original filesystem path relative to source directory.
    /// </summary>
    /// <example><c>"01 Events/Fireworks"</c></example>
    [JsonPropertyName("path")]
    public required RelativePath Path { get; init; }

    /// <summary>
    /// Optional description from front matter.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Optional cover image reference, relative to the gallery directory.
    /// </summary>
    /// <example><c>"hero.jpg"</c> or <c>"covers/hero.jpg"</c></example>
    [JsonPropertyName("cover")]
    public RelativePath? Cover { get; init; }



    /// <summary>
    /// Whether this item is hidden from navigation.
    /// </summary>
    [JsonPropertyName("hidden")]
    public bool Hidden { get; init; }

    /// <summary>
    /// Whether this item should appear in header navigation.
    /// </summary>
    [JsonPropertyName("pinned")]
    public bool Pinned { get; init; }


    /// <summary>
    /// Optional custom template for rendering (e.g., "statistics/overview").
    /// </summary>
    [JsonPropertyName("template")]
    public string? Template { get; init; }

    /// <summary>
    /// Data sources for template rendering from frontmatter.
    /// Maps variable names to data source paths (e.g., <c>{ "statistics": "statistics.json" }</c>).
    /// </summary>
    [JsonPropertyName("dataSources")]
    public IReadOnlyDictionary<string, string> DataSources { get; init; } = new Dictionary<string, string>();


    /// <summary>
    /// Images of this page in display order (configured sort or the page's filter).
    /// Empty for navigation branches without a page.
    /// </summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<ImageContent> Content { get; init; } = [];

    /// <summary>
    /// Child nodes (sub-galleries or branch sections).
    /// </summary>
    [JsonPropertyName("children")]
    public IReadOnlyList<ManifestEntry> Children { get; init; } = [];
}
