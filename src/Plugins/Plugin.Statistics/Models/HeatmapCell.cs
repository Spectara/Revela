namespace Spectara.Revela.Plugin.Statistics.Models;

/// <summary>
/// A single cell in the photo activity heatmap grid (one month of one year)
/// </summary>
internal sealed record HeatmapCell
{
    /// <summary>
    /// Year (e.g., 2025)
    /// </summary>
    public required int Year { get; init; }

    /// <summary>
    /// Month (1–12)
    /// </summary>
    public required int Month { get; init; }

    /// <summary>
    /// Number of photos taken in this month
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Intensity level (0–4) for CSS styling.
    /// 0 = no photos, 1–4 = quartile-based intensity.
    /// </summary>
    public required int Level { get; init; }
}
