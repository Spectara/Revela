using System.Text.Json.Serialization;

namespace Spectara.Revela.Plugins.Calendar.Models;

/// <summary>
/// Localized labels for calendar status display.
/// </summary>
public sealed class CalendarLabels
{
    [JsonPropertyName("booked")]
    public string Booked { get; init; } = "Booked";

    [JsonPropertyName("free")]
    public string Free { get; init; } = "Free";

    [JsonPropertyName("arrive")]
    public string Arrive { get; init; } = "Arrival";

    [JsonPropertyName("depart")]
    public string Depart { get; init; } = "Departure";
}
