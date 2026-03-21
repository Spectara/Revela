using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Plugins.Calendar.Commands;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Calendar;

/// <summary>
/// Calendar plugin for Revela — generates availability calendars from iCal data.
/// </summary>
/// <remarks>
/// Reads local .ics files (placed by Source.Calendar or manually) and produces
/// calendar.json data for Scriban templates. No HTTP — data fetching is handled
/// by the separate Source.Calendar plugin.
/// </remarks>
public sealed class CalendarPlugin : IPlugin
{
    /// <inheritdoc />
    public PluginMetadata Metadata => new()
    {
        Name = "Calendar",
        Version = "1.0.0",
        Description = "Generate availability calendars from iCal data",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<CalendarGenerateStep>();
        services.AddTransient<CleanCalendarCommand>();

        // Register as IGenerateStep for pipeline orchestration
        services.AddTransient<IGenerateStep, CalendarGenerateStep>();

        // Register as ICleanStep for clean pipeline
        services.AddTransient<ICleanStep, CleanCalendarCommand>();

        // Register page template for 'revela create page calendar'
        services.AddSingleton<IPageTemplate, CalendarPageTemplate>();
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        var calendarCommand = services.GetRequiredService<CalendarGenerateStep>();

        // Register: revela generate calendar
        yield return new CommandDescriptor(
            calendarCommand.Create(),
            ParentCommand: "generate",
            Order: 15,
            IsSequentialStep: true);

        // Register: revela clean calendar
        var cleanCalendarCommand = services.GetRequiredService<CleanCalendarCommand>();
        yield return new CommandDescriptor(
            cleanCalendarCommand.Create(),
            ParentCommand: "clean",
            Order: CleanCalendarCommand.MenuOrder,
            IsSequentialStep: true);
    }
}
