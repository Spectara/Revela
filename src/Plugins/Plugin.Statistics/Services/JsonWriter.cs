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
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Convert to template-friendly format
        var templateData = new StatisticsTemplateData
        {
            Title = "Photo Statistics",
            Description = "EXIF statistics from your photo library",
            TotalImages = statistics.TotalImages,
            ImagesWithExif = statistics.ImagesWithExif,
            Cameras = [.. statistics.CameraModels.Select(e => new ChartEntry
            {
                Name = e.Label,
                Count = e.Count,
                Percentage = e.Percent
            })],
            Lenses = [.. statistics.LensModels.Select(e => new ChartEntry
            {
                Name = e.Label,
                Count = e.Count,
                Percentage = e.Percent
            })],
            Apertures = [.. statistics.Apertures.Select(e => new ChartEntry
            {
                Value = e.Label,
                Count = e.Count,
                Percentage = e.Percent
            })],
            FocalLengths = [.. statistics.FocalLengths.Select(e => new ChartEntry
            {
                Value = e.Label.Replace("mm", "", StringComparison.OrdinalIgnoreCase),
                Count = e.Count,
                Percentage = e.Percent
            })],
            IsoValues = [.. statistics.IsoValues.Select(e => new ChartEntry
            {
                Value = e.Label,
                Count = e.Count,
                Percentage = e.Percent
            })],
            GeneratedAt = statistics.GeneratedAt
        };

        var json = JsonSerializer.Serialize(templateData, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Template-friendly statistics data structure
/// </summary>
internal sealed class StatisticsTemplateData
{
    public string Title { get; init; } = "Photo Statistics";
    public string? Description { get; init; }
    public int TotalImages { get; init; }
    public int ImagesWithExif { get; init; }
    public List<ChartEntry> Cameras { get; init; } = [];
    public List<ChartEntry> Lenses { get; init; } = [];
    public List<ChartEntry> Apertures { get; init; } = [];
    public List<ChartEntry> FocalLengths { get; init; } = [];
    public List<ChartEntry> IsoValues { get; init; } = [];
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Single chart entry for template
/// </summary>
internal sealed class ChartEntry
{
    public string? Name { get; init; }
    public string? Value { get; init; }
    public int Count { get; init; }
    public int Percentage { get; init; }
}
