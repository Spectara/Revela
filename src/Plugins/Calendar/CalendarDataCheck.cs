using Spectara.Revela.Plugins.Calendar.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Plugins.Calendar;

/// <summary>
/// Checks that every calendar page's referenced local calendar data file is present and
/// parseable, so <c>revela check</c> reports the problem before a <c>generate</c> run fails on it.
/// </summary>
/// <remarks>
/// <para>
/// The calendar plugin renders a page from a local <c>.ics</c> file during <c>generate</c>. If a
/// page references a file that is missing or invalid, generation fails rather than creating
/// misleading free availability. This check surfaces the same parser failure up front,
/// contributing to <c>revela check</c> (as <c>check calendar</c>) and the <c>check all</c> report.
/// </para>
/// <para>
/// Scope: this checks the <em>local generate input</em> only. It deliberately does not look at the
/// feed URL — fetching the feed is <c>sync</c>/<c>fetch</c>'s concern, not <c>generate</c>'s.
/// </para>
/// </remarks>
internal sealed class CalendarDataCheck(IPathResolver pathResolver) : ICheck
{
    private const string IndexFileName = "_index.revela";

    /// <inheritdoc />
    public string Name => "calendar";

    /// <inheritdoc />
    public string Title => "Calendar data";

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
        try
        {
            _ = ICalParser.Parse(icsContent);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string RelativeToSource(string source, string file) =>
        Path.GetRelativePath(source, file).Replace('\\', '/');
}
