using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Statistics;

/// <summary>
/// Artifact identifiers published by the Statistics plugin.
/// </summary>
public static class StatisticsArtifacts
{
    /// <summary>
    /// Generated statistics data consumed by themes and plugins: <c>.revela/statistics/&lt;page&gt;/statistics.json</c>
    /// (<see cref="ArtifactKind.Cache"/>, rebuilt from the manifest).
    /// </summary>
    public static ArtifactId Data { get; } =
        new("statistics/data");
}
