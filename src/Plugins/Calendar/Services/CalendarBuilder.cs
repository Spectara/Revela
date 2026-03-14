using System.Collections.Frozen;
using System.Globalization;

using Spectara.Revela.Plugins.Calendar.Models;

namespace Spectara.Revela.Plugins.Calendar.Services;

/// <summary>
/// Builds calendar data from booking ranges.
/// </summary>
/// <remarks>
/// Pure logic — no I/O, fully testable.
/// Supports two modes: "nights" (vacation rental) and "days" (photographer).
/// </remarks>
internal static class CalendarBuilder
{
    /// <summary>
    /// Builds calendar data from booking ranges.
    /// </summary>
    public static CalendarData Build(
        IReadOnlyList<BookingRange> bookings,
        int monthCount,
        DateOnly today,
        CalendarMode mode,
        CalendarLabels labels,
        CultureInfo? locale = null)
    {
        var culture = locale ?? CultureInfo.InvariantCulture;

        // Expand booking ranges into a set of booked days
        var bookedSet = ExpandBookings(bookings).ToFrozenSet();

        var startMonth = new DateOnly(today.Year, today.Month, 1);
        var months = new List<CalendarMonth>(monthCount);

        for (var i = 0; i < monthCount; i++)
        {
            var current = startMonth.AddMonths(i);
            months.Add(BuildMonth(current.Year, current.Month, bookedSet, today, mode, culture));
        }

        return new CalendarData
        {
            Labels = labels,
            Months = months
        };
    }

    /// <summary>
    /// Expands booking ranges into individual booked dates.
    /// DTSTART inclusive, DTEND exclusive (iCal spec).
    /// </summary>
    private static HashSet<DateOnly> ExpandBookings(IReadOnlyList<BookingRange> bookings)
    {
        var set = new HashSet<DateOnly>();
        foreach (var booking in bookings)
        {
            var day = booking.Start;
            while (day < booking.End)
            {
                set.Add(day);
                day = day.AddDays(1);
            }
        }

        return set;
    }

    private static CalendarMonth BuildMonth(
        int year, int month,
        FrozenSet<DateOnly> bookedSet, DateOnly today,
        CalendarMode mode, CultureInfo culture)
    {
        var weeks = BuildWeeks(year, month, bookedSet, today, mode);
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy", culture);

        return new CalendarMonth
        {
            Name = monthName,
            Weeks = weeks
        };
    }

    /// <summary>
    /// Builds weeks for a month. Each week is 7 days (Mon-Sun).
    /// Empty padding cells have number = 0.
    /// </summary>
    private static List<IReadOnlyList<CalendarDay>> BuildWeeks(
        int year, int month,
        FrozenSet<DateOnly> bookedSet, DateOnly today,
        CalendarMode mode)
    {
        var weeks = new List<IReadOnlyList<CalendarDay>>();
        var firstDay = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        // Monday = 0, Sunday = 6
        var startDayOfWeek = ((int)firstDay.DayOfWeek + 6) % 7;

        var currentWeek = new List<CalendarDay>(7);

        // Leading empty cells
        for (var i = 0; i < startDayOfWeek; i++)
        {
            currentWeek.Add(EmptyDay);
        }

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            currentWeek.Add(BuildDay(day, date, bookedSet, today, mode));

            if (currentWeek.Count == 7)
            {
                weeks.Add(currentWeek);
                currentWeek = new List<CalendarDay>(7);
            }
        }

        // Trailing empty cells
        if (currentWeek.Count > 0)
        {
            while (currentWeek.Count < 7)
            {
                currentWeek.Add(EmptyDay);
            }

            weeks.Add(currentWeek);
        }

        return weeks;
    }

    /// <summary>
    /// Builds a single day entry depending on mode.
    /// </summary>
    /// <remarks>
    /// <para>Past days (before today) get css="past" regardless of booking status.</para>
    /// <para>Today gets css="today".</para>
    /// <para>Future days get their booking status as css class:</para>
    /// <list type="bullet">
    /// <item><b>Days mode:</b> "free" or "booked"</item>
    /// <item><b>Nights mode:</b> "free", "arrive", "depart", or "booked" (arrive+depart)</item>
    /// </list>
    /// </remarks>
    private static CalendarDay BuildDay(
        int dayNumber, DateOnly date,
        FrozenSet<DateOnly> bookedSet, DateOnly today,
        CalendarMode mode)
    {
        if (date < today)
        {
            return new CalendarDay { Number = dayNumber, Css = "past" };
        }

        if (date == today)
        {
            return new CalendarDay { Number = dayNumber, Css = "today" };
        }

        if (mode == CalendarMode.Days)
        {
            return new CalendarDay
            {
                Number = dayNumber,
                Css = bookedSet.Contains(date) ? "booked" : "free"
            };
        }

        // Nights mode
        var isArrive = bookedSet.Contains(date);
        var isDepart = bookedSet.Contains(date.AddDays(-1));

        var css = (isArrive, isDepart) switch
        {
            (true, true) => "booked",
            (true, false) => "arrive",
            (false, true) => "depart",
            _ => "free"
        };

        return new CalendarDay { Number = dayNumber, Css = css };
    }

    private static readonly CalendarDay EmptyDay = new() { Number = 0 };
}
