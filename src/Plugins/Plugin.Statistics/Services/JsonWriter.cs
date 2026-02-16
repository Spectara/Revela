using System.Text.Json;
using System.Text.Json.Serialization;

using Spectara.Revela.Plugin.Statistics.Models;

namespace Spectara.Revela.Plugin.Statistics.Services;

/// <summary>
/// Writes statistics data as JSON file for template consumption
/// </summary>
public static class JsonWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
        var json = JsonSerializer.Serialize(statistics, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
