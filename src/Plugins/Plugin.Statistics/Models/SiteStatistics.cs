namespace Spectara.Revela.Plugin.Statistics.Models;

/// <summary>
/// Aggregated site statistics from EXIF data
/// </summary>
public sealed class SiteStatistics
{
    /// <summary>
    /// Total number of images in the library
    /// </summary>
    public int TotalImages { get; init; }

    /// <summary>
    /// Number of images with EXIF data
    /// </summary>
    public int ImagesWithExif { get; init; }

    /// <summary>
    /// Total number of galleries
    /// </summary>
    public int TotalGalleries { get; init; }

    /// <summary>
    /// Camera models and their usage count
    /// </summary>
    /// <example>{ "Sony ILCE-7M4": 142, "Canon EOS R5": 58 }</example>
    public IReadOnlyList<StatisticsEntry> Cameras { get; init; } = [];

    /// <summary>
    /// Lens models and their usage count
    /// </summary>
    /// <example>{ "Sony FE 35mm F1.4 GM": 89, "Sony FE 85mm F1.4 GM": 67 }</example>
    public IReadOnlyList<StatisticsEntry> Lenses { get; init; } = [];

    /// <summary>
    /// Focal length distribution (bucketed by photography ranges)
    /// </summary>
    /// <example>{ "18-35mm": 600, "35-70mm": 155 }</example>
    public IReadOnlyList<StatisticsEntry> FocalLengths { get; init; } = [];

    /// <summary>
    /// Aperture distribution (bucketed by f-stop ranges)
    /// </summary>
    /// <example>{ "f/1.4-2.0": 325, "f/2.8-4.0": 200 }</example>
    public IReadOnlyList<StatisticsEntry> Apertures { get; init; } = [];

    /// <summary>
    /// ISO distribution (bucketed by sensitivity ranges)
    /// </summary>
    /// <example>{ "100-400": 165, "400-800": 80 }</example>
    public IReadOnlyList<StatisticsEntry> IsoValues { get; init; } = [];

    /// <summary>
    /// Shutter speed distribution (exact values)
    /// </summary>
    /// <example>{ "1/500": 78, "1/250": 56 }</example>
    public IReadOnlyList<StatisticsEntry> ShutterSpeeds { get; init; } = [];

    /// <summary>
    /// Images per year
    /// </summary>
    /// <example>{ "2024": 120, "2023": 80 }</example>
    public IReadOnlyList<StatisticsEntry> ImagesByYear { get; init; } = [];

    /// <summary>
    /// Images per month (aggregated across all years)
    /// </summary>
    /// <example>{ "January": 45, "July": 82 }</example>
    public IReadOnlyList<StatisticsEntry> ImagesByMonth { get; init; } = [];

    /// <summary>
    /// Image orientation distribution (Landscape, Portrait, Square)
    /// </summary>
    /// <example>{ "Landscape": 280, "Portrait": 120, "Square": 5 }</example>
    public IReadOnlyList<StatisticsEntry> Orientations { get; init; } = [];

    /// <summary>
    /// Generation timestamp
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Single statistics entry with name, count and percentage
/// </summary>
/// <remarks>
/// Percentage is relative to the maximum count in the category (0-100),
/// used for bar chart width calculation. The highest count = 100%.
/// </remarks>
public sealed record StatisticsEntry
{
    /// <summary>
    /// Display name (e.g., "f/1.4-2.0", "Sony A7IV", "2024")
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Number of images in this category
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Percentage relative to the maximum count in the category (0-100)
    /// </summary>
    public required int Percentage { get; init; }
}
