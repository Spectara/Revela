using System.Text.Json.Serialization;

namespace Spectara.Revela.Core.Models;

/// <summary>
/// Metadata for an installed plugin, persisted as plugin.meta.json
/// </summary>
public sealed class PluginMetadata
{
    /// <summary>
    /// Package ID (e.g., Spectara.Revela.Plugin.Statistics)
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Installed version (e.g., 1.2.0)
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// Installation source (nupkg, nuget, url)
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// Where it was installed from (URL or file path)
    /// </summary>
    [JsonPropertyName("installedFrom")]
    public required string InstalledFrom { get; init; }

    /// <summary>
    /// Installation timestamp (ISO 8601)
    /// </summary>
    [JsonPropertyName("installedAt")]
    public required string InstalledAt { get; init; }

    /// <summary>
    /// Package authors
    /// </summary>
    [JsonPropertyName("authors")]
    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>
    /// Package description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Package dependencies (PackageId -> Version)
    /// </summary>
    [JsonPropertyName("dependencies")]
    public IReadOnlyDictionary<string, string> Dependencies { get; init; } = new Dictionary<string, string>();
}
