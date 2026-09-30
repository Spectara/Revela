using Spectara.Revela.Plugins.Calendar.Commands;
using Spectara.Revela.Themes.Lumina.Calendar;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class CalendarPageTemplateTests
{
    [TestMethod]
    public void TemplateName_ReferencesBodyTemplateProvidedByLuminaCalendar()
    {
        var template = new CalendarPageTemplate();
        var theme = new LuminaCalendarExtension();
        var separator = template.TemplateName.IndexOf('/', StringComparison.Ordinal);

        Assert.IsGreaterThan(0, separator, template.TemplateName);
        Assert.AreEqual(theme.Prefix, template.TemplateName[..separator]);
        using var body = theme.GetFile($"Body/{template.TemplateName[(separator + 1)..]}.revela");
        Assert.IsNotNull(body, $"Lumina.Calendar does not provide template '{template.TemplateName}'.");
        Assert.IsTrue(
            theme.GetTemplateDataDefaults(template.TemplateName).ContainsKey("calendar"),
            "Created calendar pages rely on the theme's calendar.json data default.");
    }
}
