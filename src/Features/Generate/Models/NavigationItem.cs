namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Represents a navigation item for hierarchical site navigation
/// </summary>
/// <remarks>
/// Navigation items are structured hierarchically:
/// - Column: Section header (no link), contains children
/// - Branch: Category with link and children
/// - Leaf: Final gallery item (link only, no children)
///
/// Used by Scriban templates with recursive rendering:
/// <code>
/// {{~ func render_item(item) ~}}
///   {{~ case item.type ~}}
///     {{~ when "column" ~}}
///       &lt;section&gt;{{ item.text }}...
///     {{~ when "branch" ~}}
///       &lt;div&gt;{{ item.text }}...
///     {{~ when "leaf" ~}}
///       &lt;a href="{{ item.uri }}"&gt;{{ item.text }}&lt;/a&gt;
///   {{~ end ~}}
/// {{~ end ~}}
/// </code>
/// </remarks>
public sealed class NavigationItem
{
    /// <summary>
    /// Type of navigation item: "column", "branch", or "leaf"
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Display text for the navigation item
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// URL path for the navigation item (null for columns)
    /// </summary>
    /// <remarks>
    /// This is a relative path string, not a System.Uri, because:
    /// - Templates use string interpolation with basepath
    /// - Paths are relative (e.g., "gallery/2024/")
    /// - Simplifies template syntax: {{ item.url }}
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Template engine requires string paths for interpolation with basepath")]
    public string? Url { get; init; }

    /// <summary>
    /// Whether this item is the currently active page
    /// </summary>
    public bool Active { get; init; }

    /// <summary>
    /// Child navigation items (for columns and branches)
    /// </summary>
    public IReadOnlyList<NavigationItem> Children { get; init; } = [];
}
