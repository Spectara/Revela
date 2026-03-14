using Spectara.Revela.Plugins.Calendar.Models;
using Spectara.Revela.Plugins.Calendar.Services;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class FrontmatterReaderTests
{
    [TestMethod]
    public void Read_FullConfig_ParsesAllFields()
    {
        var content = """
            +++
            title = "Verfügbarkeit"
            template = "calendar/page"
            data.calendar = "calendar.json"

            calendar.source = "bookings.ics"
            calendar.months = 6
            calendar.mode = "nights"
            calendar.locale = "de"
            calendar.labels.booked = "belegt"
            calendar.labels.free = "frei"
            calendar.labels.arrive = "Anreise"
            calendar.labels.depart = "Abreise"
            +++
            ## Belegungskalender
            """;

        var config = FrontmatterReader.Read(content);

        Assert.IsNotNull(config);
        Assert.AreEqual("bookings.ics", config.Source);
        Assert.AreEqual(6, config.Months);
        Assert.AreEqual(CalendarMode.Nights, config.Mode);
        Assert.AreEqual("de", config.Locale);
        Assert.IsNotNull(config.Labels);
        Assert.AreEqual("belegt", config.Labels.Booked);
        Assert.AreEqual("frei", config.Labels.Free);
        Assert.AreEqual("Anreise", config.Labels.Arrive);
        Assert.AreEqual("Abreise", config.Labels.Depart);
    }

    [TestMethod]
    public void Read_MinimalConfig_DefaultValues()
    {
        var content = """
            +++
            title = "Availability"
            calendar.source = "bookings.ics"
            +++
            """;

        var config = FrontmatterReader.Read(content);

        Assert.IsNotNull(config);
        Assert.AreEqual("bookings.ics", config.Source);
        Assert.AreEqual(12, config.Months);
        Assert.AreEqual(CalendarMode.Days, config.Mode);
        Assert.IsNull(config.Locale);
        Assert.IsNull(config.Labels);
    }

    [TestMethod]
    public void Read_DaysMode_ParsesCorrectly()
    {
        var content = """
            +++
            calendar.source = "schedule.ics"
            calendar.mode = "days"
            +++
            """;

        var config = FrontmatterReader.Read(content);

        Assert.IsNotNull(config);
        Assert.AreEqual(CalendarMode.Days, config.Mode);
    }

    [TestMethod]
    public void Read_NoCalendarSection_ReturnsNull()
    {
        var content = """
            +++
            title = "Regular Page"
            template = "gallery"
            +++
            """;

        var config = FrontmatterReader.Read(content);

        Assert.IsNull(config);
    }

    [TestMethod]
    public void Read_CalendarWithoutSource_ReturnsNull()
    {
        var content = """
            +++
            title = "Missing Source"
            calendar.months = 6
            +++
            """;

        var config = FrontmatterReader.Read(content);

        Assert.IsNull(config);
    }

    [TestMethod]
    public void Read_NoFrontmatter_ReturnsNull()
    {
        var content = "Just some content without frontmatter";

        var config = FrontmatterReader.Read(content);

        Assert.IsNull(config);
    }

    [TestMethod]
    public void Read_EmptyContent_ReturnsNull()
    {
        Assert.IsNull(FrontmatterReader.Read(""));
        Assert.IsNull(FrontmatterReader.Read("   "));
    }

    [TestMethod]
    public void Read_PartialLabels_FillsDefaults()
    {
        var content = """
            +++
            calendar.source = "bookings.ics"
            calendar.labels.booked = "belegt"
            +++
            """;

        var config = FrontmatterReader.Read(content);

        Assert.IsNotNull(config);
        Assert.IsNotNull(config.Labels);
        Assert.AreEqual("belegt", config.Labels.Booked);
        Assert.AreEqual("Free", config.Labels.Free);
        Assert.AreEqual("Arrival", config.Labels.Arrive);
        Assert.AreEqual("Departure", config.Labels.Depart);
    }
}
