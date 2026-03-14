namespace Spectara.Revela.Plugins.Calendar.Models;

/// <summary>
/// A date range representing a booking period from an iCal VEVENT.
/// </summary>
/// <param name="Start">First booked day (DTSTART, inclusive).</param>
/// <param name="End">First free day after booking (DTEND, exclusive per iCal spec).</param>
public sealed record BookingRange(DateOnly Start, DateOnly End);
