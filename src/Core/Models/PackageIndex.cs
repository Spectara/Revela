using System.Text.Json.Serialization;

namespace Spectara.Revela.Core.Models;

/// <summary>
/// Package index stored in cache/packages.json.
/// </summary>
public sealed class PackageIndex
{
    /// <summary>
    /// When the index was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; init; }

    /// <summary>
    /// List of all indexed packages.
    /// </summary>
    [JsonPropertyName("packages")]
    public IReadOnlyList<PackageIndexEntry> Packages { get; init; } = [];
}

/// <summary>
/// Entry in the package index.
/// </summary>
public sealed class PackageIndexEntry
{
    /// <summary>
    /// NuGet package ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Package version.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// Package description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    /// <summary>
    /// Package authors.
    /// </summary>
    [JsonPropertyName("authors")]
    public string Authors { get; init; } = "";

    /// <summary>
    /// Source feed name.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// Package types (RevelaTheme, RevelaPlugin).
    /// </summary>
    /// <remarks>
    /// A package can have multiple types (e.g., theme extensions).
    /// Inferred from naming convention for remote feeds.
    /// Read from actual .nuspec for local .nupkg files.
    /// </remarks>
    [JsonPropertyName("types")]
    public IReadOnlyList<string> Types { get; init; } = [];
}

/// <summary>
/// JSON serialization context for package index.
/// </summary>
[JsonSerializable(typeof(PackageIndex))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed partial class PackageIndexJsonContext : JsonSerializerContext;
