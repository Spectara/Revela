using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Spectara.Revela.Commands.Generate.Models.Manifest;

/// <summary>
/// Manifest entry for a navigation item (serializable subset of NavigationItem).
/// </summary>
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Required for JSON deserialization")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Template engine requires string paths")]
public sealed class NavigationManifestEntry
{
    /// <summary>
    /// Display text for the navigation item.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>
    /// URL path for the navigation item.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Whether this item is hidden from navigation.
    /// </summary>
    [JsonPropertyName("hidden")]
    public bool Hidden { get; init; }

    /// <summary>
    /// Child navigation items.
    /// </summary>
    [JsonPropertyName("children")]
    public List<NavigationManifestEntry> Children { get; init; } = [];
}
