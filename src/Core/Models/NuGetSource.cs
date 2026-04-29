using System.Text.Json.Serialization;

namespace Spectara.Revela.Core.Models;

/// <summary>
/// Represents a NuGet package source
/// </summary>
public sealed class NuGetSource
{
    /// <summary>
    /// Unique name for the source (e.g., "github", "my-feed")
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// NuGet source URL or local filesystem path.
    /// </summary>
    /// <remarks>
    /// Stored as <see cref="string"/> because NuGet sources can be either remote
    /// HTTP URLs (<c>https://api.nuget.org/v3/index.json</c>) or local filesystem
    /// paths (<c>./packages</c>) — a heterogeneous mix that <see cref="Uri"/> would
    /// awkwardly conflate.
    /// </remarks>
    [JsonPropertyName("url")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "NuGet source can be local path OR remote URL")]
    public required string Url { get; init; }

    /// <summary>
    /// Whether this source is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
}
