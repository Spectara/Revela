using System.Text.Json;

using Spectara.Revela.Plugins.Statistics.Models;

namespace Spectara.Revela.Plugins.Statistics.Services;

/// <summary>
/// Writes statistics data as JSON file for template consumption
/// </summary>
internal static class JsonWriter
{
    /// <summary>
    /// Write statistics as JSON file
    /// </summary>
    /// <param name="filePath">Path to JSON file</param>
    /// <param name="statistics">Statistics data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task WriteAsync(
        string filePath,
        SiteStatistics statistics,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(statistics, StatisticsJsonContext.Default.SiteStatistics);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
