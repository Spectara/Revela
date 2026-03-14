namespace Spectara.Revela.Plugins.Calendar.Models;

/// <summary>
/// Per-page calendar configuration parsed from _index.revela frontmatter.
/// </summary>
internal sealed class CalendarPageConfig
{
    /// <summary>
    /// Path to the local .ics file (relative to the page's source directory).
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Number of months to display (default: 12).
    /// </summary>
    public int Months { get; init; } = 12;

    /// <summary>
    /// Calendar mode: "days" (default, whole days booked) or "nights" (vacation rental with arrive/depart).
    /// </summary>
    public CalendarMode Mode { get; init; } = CalendarMode.Days;

    /// <summary>
    /// Locale for month names (e.g. "de", "en"). Null = InvariantCulture.
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Label overrides (null entries fall back to global defaults).
    /// </summary>
    public CalendarLabels? Labels { get; init; }
}

/// <summary>
/// How booked periods are interpreted and displayed.
/// </summary>
internal enum CalendarMode
{
    /// <summary>
    /// A booking covers whole days: no arrive/depart distinction.
    /// DTSTART to DTEND-1 are fully booked.
    /// </summary>
    Days,

    /// <summary>
    /// A booking covers nights: arrive/depart diagonals are shown.
    /// DTSTART = arrival day, DTEND = departure day.
    /// </summary>
    Nights
}
