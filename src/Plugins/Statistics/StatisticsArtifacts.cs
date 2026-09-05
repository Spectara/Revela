using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Statistics;

/// <summary>
/// Artifact identifiers published by the Statistics plugin.
/// </summary>
public static class StatisticsArtifacts
{
    /// <summary>Generated statistics data consumed by themes and plugins.</summary>
    public static ArtifactId Data { get; } =
        new("spectara.revela.statistics/data");
}
