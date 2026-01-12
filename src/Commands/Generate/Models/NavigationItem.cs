namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Represents a navigation item for hierarchical site navigation
/// </summary>
/// <remarks>
/// <para>
/// Navigation structure is derived from properties in Scriban templates:
/// </para>
/// <list type="bullet">
///   <item><description>Column: <c>!url &amp;&amp; children.size > 0</c> - Section header without link</description></item>
///   <item><description>Branch: <c>children.size > 0</c> - Category with children (may have link)</description></item>
///   <item><description>Leaf: <c>children.size == 0</c> - Simple link without children</description></item>
/// </list>
/// <para>
/// Example Scriban template usage:
/// </para>
/// <code>
/// {{~ func render_item(item) ~}}
///   {{~ if !item.url &amp;&amp; item.children.size > 0 ~}}
///     &lt;section&gt;{{ item.text }}...&lt;/section&gt;
///   {{~ else if item.children.size > 0 ~}}
///     &lt;div&gt;{{ item.text }}...&lt;/div&gt;
///   {{~ else ~}}
///     &lt;a href="{{ item.url }}"&gt;{{ item.text }}&lt;/a&gt;
///   {{~ end ~}}
/// {{~ end ~}}
/// </code>
/// </remarks>
public sealed class NavigationItem
{
    /// <summary>
    /// Display text for the navigation item
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// URL path for the navigation item (null for items without pages)
    /// </summary>
    /// <remarks>
    /// This is a relative path string, not a System.Uri, because:
    /// <list type="bullet">
    ///   <item><description>Templates use string interpolation with basepath</description></item>
    ///   <item><description>Paths are relative (e.g., "gallery/2024/")</description></item>
    ///   <item><description>Simplifies template syntax: {{ item.url }}</description></item>
    /// </list>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Template engine requires string paths for interpolation with basepath")]
    public string? Url { get; init; }

    /// <summary>
    /// Optional description for SEO and display purposes
    /// </summary>
    /// <remarks>
    /// Loaded from _index.md frontmatter if present.
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this item is the currently active page
    /// </summary>
    /// <remarks>
    /// True when this item or any of its children is the current page.
    /// Used for expanding navigation sections and showing breadcrumb paths.
    /// </remarks>
    public bool Active { get; init; }

    /// <summary>
    /// Whether this item is the exact current page
    /// </summary>
    /// <remarks>
    /// True only when this item's URL exactly matches the current page.
    /// Use this for highlighting the current page in navigation.
    /// </remarks>
    public bool Current { get; init; }

    /// <summary>
    /// Whether this item is hidden from navigation
    /// </summary>
    /// <remarks>
    /// Hidden items are not shown in navigation menus but are still
    /// generated and accessible via direct URL.
    /// </remarks>
    public bool Hidden { get; init; }

    /// <summary>
    /// Whether this item should appear in the header navigation
    /// </summary>
    /// <remarks>
    /// Set via frontmatter: <c>pinned = true</c>.
    /// Pinned items appear in the sticky header for quick access.
    /// </remarks>
    public bool Pinned { get; init; }

    /// <summary>
    /// Child navigation items for nested structures
    /// </summary>
    public IReadOnlyList<NavigationItem> Children { get; init; } = [];
}
