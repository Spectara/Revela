using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    // Calendar data is read by page rendering: run after scan, before pages and statistics.
    private const int GenerateOrder = PipelineOrder.Scan + 50;

    // Plugin data is removed after the host's cache clean, after statistics.
    private const int CleanOrder = CleanPipelineOrder.Cache + 150;

    /// <inheritdoc />
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Calendar",
        Name = "Calendar",
        Version = "1.0.0",
        Description = "Generate availability calendars from iCal data",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddTransient<CalendarGenerateStep>();
        services.TryAddTransient<CleanCalendarCommand>();

        // Register as pipeline steps for engine orchestration
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, CalendarGenerateStep>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, CleanCalendarCommand>());

        // Register page template for 'revela create page calendar'
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPageTemplate, CalendarPageTemplate>());

        // Contribute a generate-precondition check to 'revela check' (as 'check calendar')
        // and the 'check all' report: referenced local calendar files must be present and
        // parseable. The host auto-wraps this ICheck — the plugin adds no command.
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICheck, CalendarDataCheck>());
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        var calendarCommand = services.GetRequiredService<CalendarGenerateStep>();

        // Register: revela generate calendar
        yield return new CommandDescriptor(
            calendarCommand.Create(),
            ParentCommand: "generate",
            Order: GenerateOrder,
            IsSequentialStep: true);

        // Register: revela clean calendar
        var cleanCalendarCommand = services.GetRequiredService<CleanCalendarCommand>();
        yield return new CommandDescriptor(
            cleanCalendarCommand.Create(),
            ParentCommand: "clean",
            Order: CleanOrder,
            IsSequentialStep: true);
    }
}
