using System.CommandLine;
using System.Globalization;

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
                $"Add feeds to [cyan]project.json[/] under [cyan]{SourceCalendarConfig.Section}[/]:\n\n" +
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
                    $"Available feeds: {Markup.Escape(string.Join(", ", feeds.Keys))}");
                return 1;
            }

            feedsToFetch = feeds.Where(f => f.Key.Equals(feedName, StringComparison.OrdinalIgnoreCase));
        }

        var fetchedCount = 0;
        var totalBytes = 0L;
        var selectedFeeds = feedsToFetch.ToList();
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var uniqueDestinations = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var (name, feedConfig) in selectedFeeds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(feedConfig.Url))
                {
                    throw new ArgumentException("A feed URL is required.");
                }

                var destination = ResolveOutputPath(sourcePath, feedConfig.Output);
                if (uniqueDestinations.Any(existing =>
                    destination.StartsWith(existing + Path.DirectorySeparatorChar, pathComparison) ||
                    existing.StartsWith(destination + Path.DirectorySeparatorChar, pathComparison)) ||
                    !uniqueDestinations.Add(destination))
                {
                    throw new ArgumentException("Selected output files must be distinct and cannot be parents of one another.");
                }

            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} [cyan]{Markup.Escape(name)}[/] - invalid feed output or configuration. Use a unique relative file below the source directory, without links.");
                LogFetchFailed(name, "Invalid feed output or configuration");
                return 1;
            }
        }

        foreach (var (name, feedConfig) in selectedFeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var outputPath = ResolveOutputPath(sourcePath, feedConfig.Output);
                var bytes = await fetcher.FetchAsync(feedConfig.Url, outputPath, cancellationToken);
                totalBytes += bytes;
                fetchedCount++;
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} [cyan]{Markup.Escape(name)}[/] → {Markup.Escape(feedConfig.Output)}");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} [cyan]{Markup.Escape(name)}[/] - download timed out; previous file was retained.");
                LogFetchFailed(name, "Download timed out");
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} [cyan]{Markup.Escape(name)}[/] - download failed; check the feed configuration, network and output permissions. Previous file was retained.");
                LogFetchFailed(name, ex.GetType().Name);
            }
        }

        if (fetchedCount > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Fetched {fetchedCount} feed(s)[/] ({FormatBytes(totalBytes)})");
        }

        return fetchedCount == selectedFeeds.Count ? 0 : 1;
    }

    private static string ResolveOutputPath(string sourcePath, string output)
    {
        if (string.IsNullOrWhiteSpace(output) || output.Contains(':', StringComparison.Ordinal) || output.EndsWith('/') || output.EndsWith('\\'))
        {
            throw new ArgumentException("Invalid feed output file.", nameof(output));
        }

        var normalized = output.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(normalized, root);
        var relative = Path.GetRelativePath(root, destination);
        if (Path.IsPathRooted(normalized) || Path.IsPathRooted(relative) || relative == "." || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Feed output must be below the source directory.", nameof(output));
        }

        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            try
            {
                var attributes = File.GetAttributes(current);
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                var isDestination = string.Equals(current, destination, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || isDirectory == isDestination)
                {
                    throw new ArgumentException("Feed output must have directory parents, a file destination, and no links.", nameof(output));
                }
            }
            catch (FileNotFoundException)
            {
                break;
            }
            catch (DirectoryNotFoundException)
            {
                break;
            }
        }

        return destination;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:F1} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):F1} MB")
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch feed '{Name}': {Reason}")]
    private partial void LogFetchFailed(string name, string reason);
}
