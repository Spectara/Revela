using System.CommandLine;
using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Revela;

/// <summary>
/// Command to list all NuGet feeds
/// </summary>
public sealed partial class ConfigFeedListCommand(
    ILogger<ConfigFeedListCommand> logger)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var command = new Command("list", "List all NuGet feeds");

        command.SetAction(async (_, cancellationToken) =>
        {
            return await ExecuteAsync(cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogListingFeeds(logger);
            var config = await GlobalConfigManager.LoadAsync(cancellationToken);

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
            foreach (var feed in config.Feeds)
            {
                var feedType = feed.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? "remote"
                    : "local";

                table.AddRow(
                    $"[cyan]{feed.Name}[/]",
                    $"[dim]{feed.Url}[/]",
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
