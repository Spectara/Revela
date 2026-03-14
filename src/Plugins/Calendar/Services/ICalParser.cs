using System.Globalization;

using Spectara.Revela.Plugins.Calendar.Models;

namespace Spectara.Revela.Plugins.Calendar.Services;

/// <summary>
/// Parses iCal (RFC 5545) content and extracts VEVENT date ranges.
/// </summary>
/// <remarks>
/// Minimal parser — only handles DTSTART/DTEND with VALUE=DATE (all-day events).
/// This is sufficient for booking.com and similar calendar exports.
/// </remarks>
internal static class ICalParser
{
    /// <summary>
    /// Parses iCal content and returns booking ranges.
    /// </summary>
    /// <param name="icalContent">Raw iCal file content.</param>
    /// <returns>List of booking date ranges.</returns>
    public static IReadOnlyList<BookingRange> Parse(string icalContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icalContent);

        var ranges = new List<BookingRange>();
        var lines = icalContent.AsSpan();

        var inEvent = false;
        DateOnly? dtStart = null;
        DateOnly? dtEnd = null;

        foreach (var rawLine in lines.EnumerateLines())
        {
            var line = rawLine.Trim();

            if (line.IsEmpty)
            {
                continue;
            }

            if (line.SequenceEqual("BEGIN:VEVENT".AsSpan()))
            {
                inEvent = true;
                dtStart = null;
                dtEnd = null;
                continue;
            }

            if (line.SequenceEqual("END:VEVENT".AsSpan()))
            {
                if (inEvent && dtStart.HasValue && dtEnd.HasValue && dtEnd.Value > dtStart.Value)
                {
                    ranges.Add(new BookingRange(dtStart.Value, dtEnd.Value));
                }

                inEvent = false;
                continue;
            }

            if (!inEvent)
            {
                continue;
            }

            if (TryParseDateProperty(line, "DTSTART", out var start))
            {
                dtStart = start;
            }
            else if (TryParseDateProperty(line, "DTEND", out var end))
            {
                dtEnd = end;
            }
        }

        return ranges;
    }

    /// <summary>
    /// Tries to parse a date property line like "DTSTART;VALUE=DATE:20260320" or "DTSTART:20260320".
    /// </summary>
    private static bool TryParseDateProperty(ReadOnlySpan<char> line, string propertyName, out DateOnly date)
    {
        date = default;

        var propertySpan = propertyName.AsSpan();

        if (!line.StartsWith(propertySpan, StringComparison.Ordinal))
        {
            return false;
        }

        // After the property name, expect either ':' or ';'
        var rest = line[propertySpan.Length..];

        if (rest.IsEmpty)
        {
            return false;
        }

        // Find the colon that separates parameters from the value
        var colonIndex = rest.IndexOf(':');
        if (colonIndex < 0)
        {
            return false;
        }

        var value = rest[(colonIndex + 1)..].Trim();

        // Parse DATE format: YYYYMMDD (8 chars)
        if (value.Length >= 8)
        {
            return DateOnly.TryParseExact(
                value[..8],
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
        }

        return false;
    }
}
