using System.Text.Json.Serialization;

namespace Spectara.Revela.Core.Models;

/// <summary>
/// Configuration file for NuGet sources (stored in %APPDATA%/Revela/nuget-sources.json)
/// </summary>
public sealed class NuGetSourceConfig
{
    /// <summary>
    /// List of configured NuGet sources (mutable for internal management)
    /// </summary>
    [JsonPropertyName("sources")]
#pragma warning disable CA1002 // Do not expose generic lists - internal config class
    public List<NuGetSource> Sources { get; init; } = [];
#pragma warning restore CA1002
}
