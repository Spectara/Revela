using System.Text.Json.Serialization;

namespace Spectara.Revela.Core.Models;

/// <summary>
/// Represents a package found during NuGet search
/// </summary>
public sealed class PackageSearchResult
{
    /// <summary>
    /// Package ID (e.g., Spectara.Revela.Theme.Lumina)
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Latest available version
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// Package description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Package authors
    /// </summary>
    [JsonPropertyName("authors")]
    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>
    /// Package types (e.g., RevelaPlugin, RevelaTheme)
    /// </summary>
    [JsonPropertyName("packageTypes")]
    public IReadOnlyList<string> PackageTypes { get; init; } = [];

    /// <summary>
    /// NuGet source name where this package was found
    /// </summary>
    [JsonPropertyName("sourceName")]
    public required string SourceName { get; init; }

    /// <summary>
    /// Total download count (if available)
    /// </summary>
    [JsonPropertyName("downloadCount")]
    public long? DownloadCount { get; init; }
}
