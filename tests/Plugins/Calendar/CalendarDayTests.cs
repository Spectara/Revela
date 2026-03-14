using Spectara.Revela.Plugins.Calendar.Models;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class CalendarDayTests
{
    [TestMethod]
    public void EmptyDay_HasNumberZero()
    {
        var day = new CalendarDay { Number = 0 };

        Assert.AreEqual(0, day.Number);
        Assert.AreEqual(string.Empty, day.Css);
    }

    [TestMethod]
    public void Day_DefaultCss_IsEmpty()
    {
        var day = new CalendarDay { Number = 15 };

        Assert.AreEqual(string.Empty, day.Css);
    }

    [TestMethod]
    public void Day_WithCss_RetainsValue()
    {
        var day = new CalendarDay { Number = 20, Css = "booked" };

        Assert.AreEqual(20, day.Number);
        Assert.AreEqual("booked", day.Css);
    }
}
