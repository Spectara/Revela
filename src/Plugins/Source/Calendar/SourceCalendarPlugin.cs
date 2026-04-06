using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Plugins.Source.Calendar.Commands;
using Spectara.Revela.Plugins.Source.Calendar.Configuration;
using Spectara.Revela.Plugins.Source.Calendar.Services;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Source.Calendar;

/// <summary>
/// Source plugin for fetching iCal feeds.
/// </summary>
/// <remarks>
/// Downloads .ics files from configured URLs and saves them to the source directory.
/// The Calendar plugin then reads these local files during generation.
/// </remarks>
public sealed class SourceCalendarPlugin : IPlugin
{
    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Source.Calendar",
        Name = "Source Calendar",
        Version = "1.0.0",
        Description = "Fetch iCal feeds for calendar data",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddPluginConfig<SourceCalendarConfig>();

        services.AddHttpClient<ICalFetcher>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0 (Static Site Generator)");
        });

        services.AddTransient<CalendarFetchCommand>();
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        var fetchCommand = services.GetRequiredService<CalendarFetchCommand>();

        var calendarCommand = new Command("calendar", "iCal feed source");
        calendarCommand.Subcommands.Add(fetchCommand.Create());

        yield return new CommandDescriptor(
            calendarCommand,
            ParentCommand: "source",
            Order: 30);
    }
}
