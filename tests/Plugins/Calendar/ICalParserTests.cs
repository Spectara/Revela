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
    public void Parse_EventWithoutDtEnd_ThrowsFormatException()
    {
        var ical = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            DTSTART;VALUE=DATE:20260320
            UID:no-end@test.com
            END:VEVENT
            END:VCALENDAR
            """;

        Assert.ThrowsExactly<FormatException>(() => ICalParser.Parse(ical));
    }

    [TestMethod]
    public void Parse_EventWithSameStartAndEnd_ThrowsFormatException()
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

        Assert.ThrowsExactly<FormatException>(() => ICalParser.Parse(ical));
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
    public void Parse_FoldedProperties_ReturnsWholeBooking()
    {
        var content = SingleEventIcal.Replace("20260320", "2026\r\n 0320", StringComparison.Ordinal)
            .Replace("Not available", "Not\r\n\t available", StringComparison.Ordinal);

        var ranges = ICalParser.Parse(content);

        Assert.HasCount(1, ranges);
        Assert.AreEqual(new DateOnly(2026, 3, 20), ranges[0].Start);
    }

    [TestMethod]
    [DataRow("<html>upstream error</html>")]
    [DataRow("BEGIN:VCALENDAR")]
    [DataRow("BEGIN:VCALENDAR\nBEGIN:VEVENT\nEND:VCALENDAR")]
    [DataRow("BEGIN:VCALENDAR\nEND:VEVENT\nEND:VCALENDAR")]
    [DataRow("BEGIN:VCALENDAR\nEND:VCALENDAR\nUnexpected content")]
    [DataRow("BEGIN:VCALENDAR\nBEGIN:VCALENDAR\nEND:VCALENDAR\nEND:VCALENDAR")]
    [DataRow("BEGIN:VCALENDAR\nBEGIN: VEVENT\nDTSTART:20260320\nDTEND:20260322\nEND: VEVENT\nEND:VCALENDAR")]
    [DataRow("BEGIN:VCALENDAR\nBEGIN:VEVEN\nDTSTART:20260320\nDTEND:20260322\nEND:VEVEN\nEND:VCALENDAR")]
    [DataRow("BEGIN:VCALENDAR\nBEGIN :VEVENT\nDTSTART:20260320\nDTEND:20260322\nEND :VEVENT\nEND:VCALENDAR")]
    [DataRow("BEGIN:VCALENDAR\nBEGIN;VALUE=TEXT:VEVENT\nDTSTART:20260320\nDTEND:20260322\nEND;VALUE=TEXT:VEVENT\nEND:VCALENDAR")]
    public void Parse_InvalidDocument_ThrowsFormatException(string content) =>
        Assert.ThrowsExactly<FormatException>(() => ICalParser.Parse(content));

    [TestMethod]
    [DataRow("DTSTART:20260230\nDTEND:20260322")]
    [DataRow("DTSTART:20260320T120000Z\nDTEND:20260322T120000Z")]
    [DataRow("DTSTART:20260320\nDTSTART:20260321\nDTEND:20260322")]
    [DataRow("DTSTART:20260320\nDTEND:20260319")]
    [DataRow("DTSTART:20260320\nDTEND:20260322\nRRULE:FREQ=WEEKLY")]
    [DataRow("DTSTART:20260320\nDTEND:20260322\nEXRULE:FREQ=WEEKLY")]
    [DataRow("DTSTART:20260320\nDTEND:20260322\nDURATION:P7D")]
    public void Parse_InvalidEventAfterValidBooking_RejectsWholeCalendar(string properties)
    {
        var content = SingleEventIcal.Replace("END:VCALENDAR", $"BEGIN:VEVENT\n{properties}\nEND:VEVENT\nEND:VCALENDAR", StringComparison.Ordinal);

        Assert.ThrowsExactly<FormatException>(() => ICalParser.Parse(content));
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
