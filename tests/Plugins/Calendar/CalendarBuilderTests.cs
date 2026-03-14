using System.Globalization;

using Spectara.Revela.Plugins.Calendar.Models;
using Spectara.Revela.Plugins.Calendar.Services;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class CalendarBuilderTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly CalendarLabels DefaultLabels = new();

    [TestMethod]
    public void Build_NoBookings_AllFutureDaysFree()
    {
        var result = CalendarBuilder.Build([], 1, Today, CalendarMode.Days, DefaultLabels);

        Assert.AreEqual(1, result.Months.Count);

        var futureDays = result.Months[0].Weeks
            .SelectMany(w => w)
            .Where(d => d.Number > 14);

        Assert.IsTrue(futureDays.All(d => d.Css == "free"));
    }

    [TestMethod]
    public void Build_DaysMode_BookedAndFree()
    {
        var bookings = new[] { new BookingRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22)) };

        var result = CalendarBuilder.Build(bookings, 1, Today, CalendarMode.Days, DefaultLabels);
        var allDays = result.Months[0].Weeks.SelectMany(w => w).ToList();

        var day19 = allDays.First(d => d.Number == 19);
        var day20 = allDays.First(d => d.Number == 20);
        var day21 = allDays.First(d => d.Number == 21);
        var day22 = allDays.First(d => d.Number == 22);

        Assert.AreEqual("free", day19.Css);
        Assert.AreEqual("booked", day20.Css);
        Assert.AreEqual("booked", day21.Css);
        Assert.AreEqual("free", day22.Css); // DTEND exclusive
    }

    [TestMethod]
    public void Build_NightsMode_ArriveAndDepart()
    {
        var bookings = new[] { new BookingRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22)) };

        var result = CalendarBuilder.Build(bookings, 1, Today, CalendarMode.Nights, DefaultLabels);
        var allDays = result.Months[0].Weeks.SelectMany(w => w).ToList();

        var day19 = allDays.First(d => d.Number == 19);
        var day20 = allDays.First(d => d.Number == 20);
        var day21 = allDays.First(d => d.Number == 21);
        var day22 = allDays.First(d => d.Number == 22);
        var day23 = allDays.First(d => d.Number == 23);

        Assert.AreEqual("free", day19.Css);
        Assert.AreEqual("arrive", day20.Css);   // this night booked, prev free
        Assert.AreEqual("booked", day21.Css);    // both nights booked
        Assert.AreEqual("depart", day22.Css);    // prev night booked, this free
        Assert.AreEqual("free", day23.Css);
    }

    [TestMethod]
    public void Build_NightsMode_ChangeoverDay()
    {
        var bookings = new[]
        {
            new BookingRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22)),
            new BookingRange(new DateOnly(2026, 3, 22), new DateOnly(2026, 3, 25))
        };

        var result = CalendarBuilder.Build(bookings, 1, Today, CalendarMode.Nights, DefaultLabels);
        var day22 = result.Months[0].Weeks.SelectMany(w => w).First(d => d.Number == 22);

        // Depart (prev booked) + arrive (this booked) = fully booked
        Assert.AreEqual("booked", day22.Css);
    }

    [TestMethod]
    public void Build_PastDays_CssPast()
    {
        var result = CalendarBuilder.Build([], 1, Today, CalendarMode.Days, DefaultLabels);
        var allDays = result.Months[0].Weeks.SelectMany(w => w).Where(d => d.Number > 0).ToList();

        // Days before today = "past" (regardless of booking)
        var pastDays = allDays.Where(d => d.Number < 14);
        Assert.IsTrue(pastDays.All(d => d.Css == "past"));
    }

    [TestMethod]
    public void Build_PastBookedDay_StillShowsPast()
    {
        // Booking in the past — should still be "past", not "booked"
        var bookings = new[] { new BookingRange(new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 8)) };

        var result = CalendarBuilder.Build(bookings, 1, Today, CalendarMode.Days, DefaultLabels);
        var day6 = result.Months[0].Weeks.SelectMany(w => w).First(d => d.Number == 6);

        Assert.AreEqual("past", day6.Css);
    }

    [TestMethod]
    public void Build_Today_CssToday()
    {
        var result = CalendarBuilder.Build([], 1, Today, CalendarMode.Days, DefaultLabels);
        var todayDay = result.Months[0].Weeks.SelectMany(w => w).First(d => d.Number == 14);

        Assert.AreEqual("today", todayDay.Css);
    }

    [TestMethod]
    public void Build_EmptyDays_NumberZero()
    {
        var result = CalendarBuilder.Build([], 1, Today, CalendarMode.Days, DefaultLabels);
        var firstWeek = result.Months[0].Weeks[0];

        // March 2026 starts on Sunday, so Mon-Sat are empty
        for (var i = 0; i < 6; i++)
        {
            Assert.AreEqual(0, firstWeek[i].Number);
        }

        Assert.AreEqual(1, firstWeek[6].Number);
    }

    [TestMethod]
    public void Build_WeeksHaveSevenDays()
    {
        var result = CalendarBuilder.Build([], 1, Today, CalendarMode.Days, DefaultLabels);
        Assert.IsTrue(result.Months[0].Weeks.All(w => w.Count == 7));
    }

    [TestMethod]
    public void Build_TwelveMonths_GeneratesAll()
    {
        var result = CalendarBuilder.Build([], 12, Today, CalendarMode.Days, DefaultLabels);
        Assert.AreEqual(12, result.Months.Count);
    }

    [TestMethod]
    public void Build_WithGermanLocale_FormatsMonthName()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        var result = CalendarBuilder.Build([], 1, Today, CalendarMode.Days, DefaultLabels, culture);

        Assert.IsTrue(result.Months[0].Name.StartsWith('M'));
        Assert.IsTrue(result.Months[0].Name.Contains("2026", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_LabelsPassedThrough()
    {
        var labels = new CalendarLabels
        {
            Booked = "belegt",
            Free = "frei",
            Arrive = "Anreise",
            Depart = "Abreise"
        };

        var result = CalendarBuilder.Build([], 1, Today, CalendarMode.Days, labels);

        Assert.AreEqual("belegt", result.Labels.Booked);
        Assert.AreEqual("frei", result.Labels.Free);
    }
}
