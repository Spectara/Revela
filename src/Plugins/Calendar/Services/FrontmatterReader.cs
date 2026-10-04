using Scriban.Runtime;

using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Plugins.Calendar.Models;

namespace Spectara.Revela.Plugins.Calendar.Services;

/// <summary>
/// Reads calendar.* frontmatter fields from _index.revela files.
/// </summary>
/// <remarks>
/// Evaluates the frontmatter with the core's <see cref="FrontMatterEvaluator"/> (linked source),
/// so calendar.* keys follow the same rules as every other key: a failing statement is
/// skipped and dotted keys get their parent objects.
/// </remarks>
internal static class FrontmatterReader
{
    /// <summary>
    /// Reads calendar configuration from _index.revela frontmatter.
    /// </summary>
    /// <param name="content">Raw _index.revela file content.</param>
    /// <returns>Calendar page config, or null if no calendar section found.</returns>
    public static CalendarPageConfig? Read(string content)
    {
        if (FrontMatterEvaluator.Evaluate(content) is not { } global)
        {
            return null;
        }

        if (!global.TryGetValue("calendar", out var calendarValue) || calendarValue is not ScriptObject calendarObj)
        {
            return null;
        }

        var source = GetString(calendarObj, "source");

        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        var labels = ReadLabels(calendarObj);
        var modeStr = GetString(calendarObj, "mode");
        var mode = modeStr?.Equals("nights", StringComparison.OrdinalIgnoreCase) is true
            ? CalendarMode.Nights
            : CalendarMode.Days;

        return new CalendarPageConfig
        {
            Source = source,
            Months = GetInt(calendarObj, "months", 12),
            Mode = mode,
            Locale = GetString(calendarObj, "locale"),
            Labels = labels
        };
    }

    private static CalendarLabels? ReadLabels(ScriptObject calendarObj)
    {
        if (!calendarObj.TryGetValue("labels", out var labelsValue) || labelsValue is not ScriptObject labelsObj)
        {
            return null;
        }

        // Unknown label keys alone don't count as labels
        var booked = GetString(labelsObj, "booked");
        var free = GetString(labelsObj, "free");
        var arrive = GetString(labelsObj, "arrive");
        var depart = GetString(labelsObj, "depart");

        if (booked is null && free is null && arrive is null && depart is null)
        {
            return null;
        }

        return new CalendarLabels
        {
            Booked = booked,
            Free = free,
            Arrive = arrive,
            Depart = depart
        };
    }

    private static string? GetString(ScriptObject obj, string key) =>
        obj.TryGetValue(key, out var value) && value is string str && !string.IsNullOrEmpty(str)
            ? str
            : null;

    private static int GetInt(ScriptObject obj, string key, int defaultValue) =>
        obj.TryGetValue(key, out var value)
            ? value switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => defaultValue
            }
            : defaultValue;
}
