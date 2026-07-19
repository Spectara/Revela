using Spectara.Revela.Plugins.Calendar.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Plugins.Calendar;

/// <summary>
/// Validates the <c>generate</c> precondition that every calendar page's referenced local
/// calendar data file is present and parseable, before the pipeline reaches the calendar step.
/// </summary>
/// <remarks>
/// <para>
/// The calendar plugin renders a page from a local <c>.ics</c> file during <c>generate</c>. If a
/// page references a file that is missing or not a valid iCalendar object, the calendar step would
/// silently skip it and produce a broken (empty) calendar. This validator surfaces that up front.
/// </para>
/// <para>
/// Scope: this checks the <em>local generate input</em> only. It deliberately does not look at the
/// feed URL — fetching the feed is <c>sync</c>/<c>fetch</c>'s concern, not <c>generate</c>'s.
/// </para>
/// </remarks>
internal sealed class CalendarDataValidator(IPathResolver pathResolver) : IValidator
{
    private const string IndexFileName = "_index.revela";
    private const string CalendarMarker = "BEGIN:VCALENDAR";

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var source = pathResolver.SourcePath;
        if (!Directory.Exists(source))
        {
            return [];
        }

        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var indexPath in Directory.EnumerateFiles(source, IndexFileName, SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string indexContent;
            try
            {
                indexContent = await File.ReadAllTextAsync(indexPath, cancellationToken);
            }
            catch (IOException)
            {
                continue;
            }

            var pageConfig = FrontmatterReader.Read(indexContent);
            if (pageConfig is null)
            {
                continue;
            }

            var pageDir = Path.GetDirectoryName(indexPath)!;
            var icsPath = Path.Combine(pageDir, pageConfig.Source);
            var relativeIcs = RelativeToSource(source, icsPath);

            if (!File.Exists(icsPath))
            {
                diagnostics.Add(ValidationDiagnostic.Error(
                    $"Calendar page references a missing calendar file: {pageConfig.Source}",
                    file: relativeIcs,
                    hint: "Run 'revela source calendar fetch' to download the feed, or place the .ics file next to the page."));
                continue;
            }

            string icsContent;
            try
            {
                icsContent = await File.ReadAllTextAsync(icsPath, cancellationToken);
            }
            catch (IOException ex)
            {
                diagnostics.Add(ValidationDiagnostic.Error(
                    $"Calendar file could not be read: {ex.Message}",
                    file: relativeIcs,
                    hint: "Check the file's permissions, then run the command again."));
                continue;
            }

            if (!IsParseable(icsContent))
            {
                diagnostics.Add(ValidationDiagnostic.Error(
                    "Calendar file is not a valid iCalendar document.",
                    file: relativeIcs,
                    hint: "The file must be iCal (RFC 5545) data beginning with 'BEGIN:VCALENDAR' — re-export or re-fetch it."));
            }
        }

        return diagnostics;
    }

    private static bool IsParseable(string icsContent)
    {
        if (string.IsNullOrWhiteSpace(icsContent) ||
            !icsContent.Contains(CalendarMarker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            _ = ICalParser.Parse(icsContent);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string RelativeToSource(string source, string file) =>
        Path.GetRelativePath(source, file).Replace('\\', '/');
}
