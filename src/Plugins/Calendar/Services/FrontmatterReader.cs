using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

using Spectara.Revela.Plugins.Calendar.Models;

namespace Spectara.Revela.Plugins.Calendar.Services;

/// <summary>
/// Reads calendar.* frontmatter fields from _index.revela files.
/// </summary>
/// <remarks>
/// Re-parses the frontmatter using Scriban to extract calendar-specific fields
/// that the core RevelaParser does not expose. This keeps the plugin self-contained.
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
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("+++", StringComparison.Ordinal))
        {
            return null;
        }

        // Ensure content ends with newline — Scriban requires this for frontmatter parsing
        var normalizedContent = content;
        if (!content.EndsWith('\n'))
        {
            normalizedContent = content + "\n";
        }

        var lexerOptions = new LexerOptions
        {
            Mode = ScriptMode.FrontMatterAndContent,
            FrontMatterMarker = "+++"
        };

        var template = Template.Parse(normalizedContent, lexerOptions: lexerOptions);

        if (template.HasErrors)
        {
            return null;
        }

        var context = new TemplateContext
        {
            StrictVariables = false
        };

        // Pre-seed nested objects so Scriban can assign into them
        // (e.g., calendar.source = "..." requires calendar to exist as an object)
        var global = (ScriptObject)context.CurrentGlobal;
        global["calendar"] = new ScriptObject
        {
            ["labels"] = new ScriptObject()
        };
        global["data"] = new ScriptObject();

        if (template.Page.FrontMatter is not null)
        {
            context.Evaluate(template.Page.FrontMatter);
        }

        // Re-read global after evaluation
        global = (ScriptObject)context.CurrentGlobal;

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

        // Check if any label was actually set (vs. empty seed object)
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
            Booked = booked ?? "Booked",
            Free = free ?? "Free",
            Arrive = arrive ?? "Arrival",
            Depart = depart ?? "Departure"
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
