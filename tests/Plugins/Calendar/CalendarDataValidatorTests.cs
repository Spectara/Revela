using NSubstitute;

using Spectara.Revela.Plugins.Calendar;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class CalendarDataValidatorTests
{
    private const string ValidIcs =
        "BEGIN:VCALENDAR\r\n" +
        "VERSION:2.0\r\n" +
        "BEGIN:VEVENT\r\n" +
        "DTSTART;VALUE=DATE:20260320\r\n" +
        "DTEND;VALUE=DATE:20260322\r\n" +
        "END:VEVENT\r\n" +
        "END:VCALENDAR\r\n";

    [TestMethod]
    public async Task ValidateAsync_ReferencedCalendarFileMissing_ReturnsError()
    {
        using var source = new TempSource();
        source.WriteCalendarPage("availability", "availability.ics");
        // No .ics file written — the referenced generate input is missing.

        var diagnostics = await CreateValidator(source).ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("missing", StringComparison.OrdinalIgnoreCase)),
            "A missing referenced calendar file must produce an error.");
    }

    [TestMethod]
    public async Task ValidateAsync_ReferencedCalendarFilePresentAndValid_ReturnsNoDiagnostics()
    {
        using var source = new TempSource();
        source.WriteCalendarPage("availability", "availability.ics");
        source.WriteFile("availability", "availability.ics", ValidIcs);

        var diagnostics = await CreateValidator(source).ValidateAsync();

        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task ValidateAsync_ReferencedCalendarFileUnparseable_ReturnsError()
    {
        using var source = new TempSource();
        source.WriteCalendarPage("availability", "availability.ics");
        source.WriteFile("availability", "availability.ics", "<html>not a calendar</html>");

        var diagnostics = await CreateValidator(source).ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("iCalendar", StringComparison.OrdinalIgnoreCase)),
            "An unparseable calendar file must produce an error.");
    }

    [TestMethod]
    public async Task ValidateAsync_NoCalendarPages_ReturnsNoDiagnostics()
    {
        using var source = new TempSource();
        // A plain (non-calendar) page must be ignored entirely.
        source.WriteFile("landscapes", "_index.revela", "+++\ntitle = \"Landscapes\"\n+++\n");

        var diagnostics = await CreateValidator(source).ValidateAsync();

        Assert.IsEmpty(diagnostics);
    }

    private static CalendarDataValidator CreateValidator(TempSource source)
    {
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.SourcePath.Returns(source.Path);
        return new CalendarDataValidator(pathResolver);
    }

    private sealed class TempSource : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "revela-cal-" + Guid.NewGuid().ToString("N"));

        public TempSource() => Directory.CreateDirectory(Path);

        public void WriteCalendarPage(string pageName, string icsFileName) =>
            WriteFile(pageName, "_index.revela", $"+++\ncalendar.source = \"{icsFileName}\"\n+++\n");

        public void WriteFile(string pageName, string fileName, string content)
        {
            var pageDir = System.IO.Path.Combine(Path, pageName);
            Directory.CreateDirectory(pageDir);
            File.WriteAllText(System.IO.Path.Combine(pageDir, fileName), content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
