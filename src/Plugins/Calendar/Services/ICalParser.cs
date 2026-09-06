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

        var unfoldedContent = icalContent
            .Replace("\r\n ", "", StringComparison.Ordinal)
            .Replace("\r\n\t", "", StringComparison.Ordinal)
            .Replace("\n ", "", StringComparison.Ordinal)
            .Replace("\n\t", "", StringComparison.Ordinal);
        var ranges = new List<BookingRange>();
        var components = new Stack<string>();
        var calendarStarted = false;
        var calendarEnded = false;
        DateOnly? dtStart = null;
        DateOnly? dtEnd = null;

        foreach (var rawLine in unfoldedContent.AsSpan().TrimStart('\uFEFF').EnumerateLines())
        {
            var line = rawLine.Trim();

            if (line.IsEmpty)
            {
                continue;
            }

            if (!calendarStarted)
            {
                if (!line.Equals("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException("Expected BEGIN:VCALENDAR.");
                }

                calendarStarted = true;
                components.Push("VCALENDAR");
                continue;
            }

            if (calendarEnded)
            {
                throw new FormatException("Unexpected content after END:VCALENDAR.");
            }

            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
            {
                throw new FormatException("Invalid iCalendar property.");
            }

            var property = line[..colonIndex];
            var parameterIndex = property.IndexOf(';');
            var propertyName = parameterIndex < 0 ? property : property[..parameterIndex];
            foreach (var character in propertyName)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '-')
                {
                    throw new FormatException("Invalid iCalendar property name.");
                }
            }

            if (propertyName.IsEmpty || (parameterIndex >= 0 &&
                (propertyName.Equals("BEGIN", StringComparison.OrdinalIgnoreCase) || propertyName.Equals("END", StringComparison.OrdinalIgnoreCase))))
            {
                throw new FormatException("Invalid iCalendar component boundary.");
            }

            if (line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase))
            {
                var component = line[6..].ToString().ToUpperInvariant();
                var allowed = components.Peek() switch
                {
                    "VCALENDAR" => component is "VEVENT" or "VTIMEZONE",
                    "VTIMEZONE" => component is "STANDARD" or "DAYLIGHT",
                    "VEVENT" => component == "VALARM",
                    _ => false
                };
                if (!allowed)
                {
                    throw new FormatException("Invalid iCalendar component nesting.");
                }

                components.Push(component);
                if (component == "VEVENT")
                {
                    dtStart = null;
                    dtEnd = null;
                }

                continue;
            }

            if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase))
            {
                if (!line[4..].Equals(components.Peek(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException("Mismatched iCalendar component boundary.");
                }

                if (components.Pop() == "VEVENT")
                {
                    if (!dtStart.HasValue || !dtEnd.HasValue || dtEnd.Value <= dtStart.Value)
                    {
                        throw new FormatException("Every booking must have a valid DTSTART and a later DTEND.");
                    }

                    ranges.Add(new BookingRange(dtStart.Value, dtEnd.Value));
                }

                calendarEnded = components.Count == 0;
                continue;
            }

            if (components.Peek() != "VEVENT")
            {
                continue;
            }

            if (propertyName.Equals("RRULE", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Equals("RDATE", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Equals("EXDATE", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Equals("EXRULE", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Equals("DURATION", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Equals("RECURRENCE-ID", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("Recurrence and duration-based bookings are not supported; export individual all-day events with DTSTART and DTEND.");
            }

            var isStart = propertyName.Equals("DTSTART", StringComparison.OrdinalIgnoreCase);
            var isEnd = propertyName.Equals("DTEND", StringComparison.OrdinalIgnoreCase);
            if (!isStart && !isEnd)
            {
                continue;
            }

            if ((parameterIndex >= 0 && !property[parameterIndex..].Equals(";VALUE=DATE", StringComparison.OrdinalIgnoreCase)) ||
                !DateOnly.TryParseExact(line[(colonIndex + 1)..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                (isStart ? dtStart.HasValue : dtEnd.HasValue))
            {
                throw new FormatException("Booking dates must be unique all-day dates in YYYYMMDD format.");
            }

            if (isStart)
            {
                dtStart = date;
            }
            else
            {
                dtEnd = date;
            }
        }

        if (!calendarEnded)
        {
            throw new FormatException("Incomplete iCalendar document.");
        }

        return ranges;
    }
}
