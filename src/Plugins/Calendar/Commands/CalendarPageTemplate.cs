using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Calendar.Commands;

/// <summary>
/// Page template for creating calendar pages with availability data.
/// </summary>
/// <remarks>
/// Generates _index.revela files with frontmatter for the calendar/page template.
/// Requires a local iCalendar (.ics) file and optionally configures display months, mode, and labels.
/// <para>
/// Usage: revela create page calendar source/availability --title "Availability"
/// </para>
/// </remarks>
public sealed class CalendarPageTemplate : IPageTemplate
{
    /// <inheritdoc />
    public string Name => "calendar";

    /// <inheritdoc />
    public string DisplayName => "Availability Calendar";

    /// <inheritdoc />
    public string Description => "Create calendar page with availability from iCal data";

    /// <inheritdoc />
    public string TemplateName => "calendar/page";

    /// <inheritdoc />
    public IReadOnlyList<TemplateProperty> PageProperties { get; } =
    [
        new()
        {
            Name = "title",
            Aliases = ["--title", "-t"],
            Type = typeof(string),
            DefaultValue = "Availability",
            Description = "Page title (example: 'Booking Calendar')",
            Required = false,
            FrontmatterKey = "title"
        },
        new()
        {
            Name = "description",
            Aliases = ["--description", "-d"],
            Type = typeof(string),
            DefaultValue = "Availability calendar",
            Description = "Page description",
            Required = false,
            FrontmatterKey = "description"
        },
        new()
        {
            Name = "source",
            Aliases = ["--source", "-s"],
            Type = typeof(string),
            DefaultValue = "",
            Description = "iCalendar (.ics) file path, relative to the page folder (example: 'bookings.ics')",
            Required = true,
            FrontmatterKey = "calendar.source"
        },
        new()
        {
            Name = "months",
            Aliases = ["--months", "-m"],
            Type = typeof(int),
            DefaultValue = 12,
            Description = "Number of months to display (default: 12)",
            Required = false,
            FrontmatterKey = "calendar.months"
        },
    ];
}
