using System.Text.Json.Serialization;

namespace Spectara.Revela.Plugins.Calendar.Models;

/// <summary>
/// Page-specific labels for calendar status display.
/// </summary>
/// <remarks>
/// Unset labels stay <see langword="null"/>; the theme then renders its own
/// translation for the site language (Lumina.Calendar: <c>calendar.free</c>, …).
/// </remarks>
public sealed class CalendarLabels
{
    [JsonPropertyName("booked")]
    public string? Booked { get; init; }

    [JsonPropertyName("free")]
    public string? Free { get; init; }

    [JsonPropertyName("arrive")]
    public string? Arrive { get; init; }

    [JsonPropertyName("depart")]
    public string? Depart { get; init; }
}
