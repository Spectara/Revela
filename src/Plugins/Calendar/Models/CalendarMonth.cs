using System.Text.Json.Serialization;

namespace Spectara.Revela.Plugins.Calendar.Models;

/// <summary>
/// A single month in the calendar.
/// </summary>
public sealed class CalendarMonth
{
    /// <summary>
    /// Formatted month name (e.g. "März 2026").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Weeks as arrays of 7 days each (Mon-Sun). Days with number=0 are padding.
    /// </summary>
    [JsonPropertyName("weeks")]
    public IReadOnlyList<IReadOnlyList<CalendarDay>> Weeks { get; init; } = [];
}
