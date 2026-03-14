using Spectara.Revela.Plugins.Calendar.Services;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class ICalParserTests
{
    private const string SingleEventIcal = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//Test//Test//EN
        BEGIN:VEVENT
        DTSTAMP:20260312T113950Z
        DTSTART;VALUE=DATE:20260320
        DTEND;VALUE=DATE:20260322
        UID:test-event-1@test.com
        SUMMARY:CLOSED - Not available
        END:VEVENT
        END:VCALENDAR
        """;

    private const string MultipleEventsIcal = """
        BEGIN:VCALENDAR
        VERSION:2.0
        BEGIN:VEVENT
        DTSTART;VALUE=DATE:20260320
        DTEND;VALUE=DATE:20260322
        UID:event-1@test.com
        END:VEVENT
        BEGIN:VEVENT
        DTSTART;VALUE=DATE:20260401
        DTEND;VALUE=DATE:20260410
        UID:event-2@test.com
        END:VEVENT
        BEGIN:VEVENT
        DTSTART;VALUE=DATE:20260501
        DTEND;VALUE=DATE:20260502
        UID:event-3@test.com
        END:VEVENT
        END:VCALENDAR
        """;

    [TestMethod]
    public void Parse_SingleEvent_ReturnsOneRange()
    {
        var ranges = ICalParser.Parse(SingleEventIcal);

        Assert.AreEqual(1, ranges.Count);
        Assert.AreEqual(new DateOnly(2026, 3, 20), ranges[0].Start);
        Assert.AreEqual(new DateOnly(2026, 3, 22), ranges[0].End);
    }

    [TestMethod]
    public void Parse_MultipleEvents_ReturnsAllRanges()
    {
        var ranges = ICalParser.Parse(MultipleEventsIcal);

        Assert.AreEqual(3, ranges.Count);
    }

    [TestMethod]
    public void Parse_DtStartWithoutValueDateParam_ParsesCorrectly()
    {
        var ical = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            DTSTART:20260315
            DTEND:20260317
            UID:simple@test.com
            END:VEVENT
            END:VCALENDAR
            """;

        var ranges = ICalParser.Parse(ical);

        Assert.AreEqual(1, ranges.Count);
        Assert.AreEqual(new DateOnly(2026, 3, 15), ranges[0].Start);
        Assert.AreEqual(new DateOnly(2026, 3, 17), ranges[0].End);
    }

    [TestMethod]
    public void Parse_EventWithoutDtEnd_IsSkipped()
    {
        var ical = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            DTSTART;VALUE=DATE:20260320
            UID:no-end@test.com
            END:VEVENT
            END:VCALENDAR
            """;

        var ranges = ICalParser.Parse(ical);

        Assert.AreEqual(0, ranges.Count);
    }

    [TestMethod]
    public void Parse_EventWithSameStartAndEnd_IsSkipped()
    {
        var ical = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            DTSTART;VALUE=DATE:20260320
            DTEND;VALUE=DATE:20260320
            UID:zero-length@test.com
            END:VEVENT
            END:VCALENDAR
            """;

        var ranges = ICalParser.Parse(ical);

        Assert.AreEqual(0, ranges.Count);
    }

    [TestMethod]
    public void Parse_EmptyCalendar_ReturnsEmptyList()
    {
        var ical = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Test//Test//EN
            END:VCALENDAR
            """;

        var ranges = ICalParser.Parse(ical);

        Assert.AreEqual(0, ranges.Count);
    }

    [TestMethod]
    public void Parse_NullOrWhitespace_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ICalParser.Parse(""));
        Assert.ThrowsExactly<ArgumentException>(() => ICalParser.Parse("   "));
    }

    [TestMethod]
    public void Parse_RealWorldBookingComFormat_ParsesCorrectly()
    {
        var ical = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Booking.com//Booking.com//EN
            CALSCALE:GREGORIAN
            BEGIN:VEVENT
            DTSTAMP:20260312T113950Z
            DTSTART;VALUE=DATE:20260320
            DTEND;VALUE=DATE:20260322
            UID:6e86b97fbe59c33cbc9a74e106722f4f@booking.com
            SUMMARY:CLOSED - Not available
            END:VEVENT
            BEGIN:VEVENT
            DTSTAMP:20260312T113950Z
            DTSTART;VALUE=DATE:20260405
            DTEND;VALUE=DATE:20260412
            UID:abc123def456@booking.com
            SUMMARY:CLOSED - Not available
            END:VEVENT
            END:VCALENDAR
            """;

        var ranges = ICalParser.Parse(ical);

        Assert.AreEqual(2, ranges.Count);
        Assert.AreEqual(new DateOnly(2026, 3, 20), ranges[0].Start);
        Assert.AreEqual(new DateOnly(2026, 3, 22), ranges[0].End);
        Assert.AreEqual(new DateOnly(2026, 4, 5), ranges[1].Start);
        Assert.AreEqual(new DateOnly(2026, 4, 12), ranges[1].End);
    }
}
