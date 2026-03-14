using System.CommandLine;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Source.Calendar.Configuration;
using Spectara.Revela.Plugins.Source.Calendar.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Source.Calendar.Commands;

/// <summary>
/// Command to fetch iCal feeds and save them to the source directory.
/// </summary>
internal sealed partial class CalendarFetchCommand(
    ILogger<CalendarFetchCommand> logger,
    ICalFetcher fetcher,
    IOptionsMonitor<SourceCalendarConfig> config,
    IPathResolver pathResolver)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("fetch", "Fetch iCal feeds and save to source directory");

        var nameOption = new Option<string?>("--name", "-n")
        {
            Description = "Fetch only the named feed (default: fetch all)"
        };
        command.Options.Add(nameOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameOption);
            return await ExecuteAsync(name, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string? feedName, CancellationToken cancellationToken)
    {
        var feeds = config.CurrentValue.Feeds;
        var sourcePath = pathResolver.SourcePath;

        if (feeds.Count == 0)
        {
            ErrorPanels.ShowWarning(
                "No Feeds Configured",
                "[yellow]No iCal feeds configured.[/]\n\n" +
                $"Add feeds to [cyan]project.json[/] under [cyan]{SourceCalendarConfig.SectionName}[/]:\n\n" +
                "[dim]\"feeds\": {\n" +
                "  \"booking\": {\n" +
                "    \"url\": \"https://ical.example.com/calendar.ics\",\n" +
                "    \"output\": \"availability/bookings.ics\"\n" +
                "  }\n" +
                "}[/]");
            return 1;
        }

        // Filter to single feed if --name specified
        var feedsToFetch = feeds.AsEnumerable();
        if (feedName is not null)
        {
            if (!feeds.ContainsKey(feedName))
            {
                ErrorPanels.ShowWarning(
                    "Feed Not Found",
                    $"[yellow]Feed '{Markup.Escape(feedName)}' not found.[/]\n\n" +
                    $"Available feeds: {string.Join(", ", feeds.Keys)}");
                return 1;
            }

            feedsToFetch = feeds.Where(f => f.Key.Equals(feedName, StringComparison.OrdinalIgnoreCase));
        }

        var fetchedCount = 0;
        var totalBytes = 0L;

        foreach (var (name, feedConfig) in feedsToFetch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(feedConfig.Url))
            {
                LogSkippingFeed(name, "no URL configured");
                continue;
            }

            var outputPath = Path.Combine(sourcePath, feedConfig.Output);

            try
            {
                var bytes = await fetcher.FetchAsync(feedConfig.Url, outputPath, cancellationToken);
                totalBytes += bytes;
                fetchedCount++;
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} [cyan]{Markup.Escape(name)}[/] → {Markup.Escape(feedConfig.Output)}");
            }
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} [cyan]{Markup.Escape(name)}[/] — {Markup.Escape(ex.Message)}");
                LogFetchFailed(name, ex);
            }
        }

        if (fetchedCount > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Fetched {fetchedCount} feed(s)[/] ({FormatBytes(totalBytes)})");
        }

        return fetchedCount > 0 ? 0 : 1;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping feed '{Name}': {Reason}")]
    private partial void LogSkippingFeed(string name, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch feed '{Name}'")]
    private partial void LogFetchFailed(string name, Exception ex);
}
