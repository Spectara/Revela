using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Revela;

/// <summary>
/// Command to list all NuGet feeds
/// </summary>
public sealed partial class ConfigFeedListCommand(
    ILogger<ConfigFeedListCommand> logger,
    IOptionsMonitor<FeedsConfig> feedsConfig)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var command = new Command("list", "List all NuGet feeds");

        command.SetAction((_, _) => Task.FromResult(Execute()));

        return command;
    }

    private int Execute()
    {
        try
        {
            LogListingFeeds(logger);
            var config = feedsConfig.CurrentValue;

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Name")
                .AddColumn("URL")
                .AddColumn("Type");

            // Built-in nuget.org
            table.AddRow(
                "[cyan]nuget.org[/]",
                "[dim]https://api.nuget.org/v3/index.json[/]",
                "[blue]built-in[/]");

            // User-configured feeds
            foreach (var (name, url) in config.Feeds)
            {
                var feedType = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? "remote"
                    : "local";

                table.AddRow(
                    $"[cyan]{name}[/]",
                    $"[dim]{url}[/]",
                    feedType == "local" ? "[green]local[/]" : "[dim]remote[/]");
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Total: [cyan]{config.Feeds.Count + 1}[/] feed(s)");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Config: [dim]{GlobalConfigManager.ConfigFilePath}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            LogListFailed(logger, ex);
            AnsiConsole.MarkupLine($"[red]ERROR[/] Failed to list feeds: {ex.Message}");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing NuGet feeds")]
    private static partial void LogListingFeeds(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to list feeds")]
    private static partial void LogListFailed(ILogger logger, Exception exception);
}
