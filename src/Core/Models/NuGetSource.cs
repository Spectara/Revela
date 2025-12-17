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
    /// NuGet v3 API URL (stored as string for JSON serialization)
    /// </summary>
    [JsonPropertyName("url")]
#pragma warning disable CA1056 // URI properties should not be strings - required for JSON serialization
    public required string Url { get; init; }
#pragma warning restore CA1056

    /// <summary>
    /// Whether this source is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
}
