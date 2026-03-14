using System.Text.Json.Serialization;

namespace Spectara.Revela.Plugins.Calendar.Models;

/// <summary>
/// A single day in the calendar. Number = 0 means empty cell (padding).
/// </summary>
public sealed class CalendarDay
{
    /// <summary>
    /// Day of the month (1-31), or 0 for empty padding cells.
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; init; }

    /// <summary>
    /// CSS class for styling: "past", "today", "free", "booked", "arrive", "depart".
    /// </summary>
    [JsonPropertyName("css")]
    public string Css { get; init; } = string.Empty;
}
