using System.Text.Json.Serialization;

namespace Spectara.Revela.Plugins.Calendar.Models;

/// <summary>
/// Root calendar data model written to calendar.json and consumed by Scriban templates.
/// </summary>
public sealed class CalendarData
{
    /// <summary>
    /// Abbreviated weekday names (Mon-Sun) in the configured locale.
    /// </summary>
    [JsonPropertyName("day_names")]
    public IReadOnlyList<string> DayNames { get; init; } = [];

    /// <summary>
    /// Localized label strings for template rendering.
    /// </summary>
    [JsonPropertyName("labels")]
    public CalendarLabels Labels { get; init; } = new();

    /// <summary>
    /// The months to display.
    /// </summary>
    [JsonPropertyName("months")]
    public IReadOnlyList<CalendarMonth> Months { get; init; } = [];
}
