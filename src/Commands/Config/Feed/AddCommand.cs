using System.CommandLine;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Feed;

/// <summary>
/// Command to add a NuGet feed.
/// </summary>
public sealed partial class AddCommand(
    ILogger<AddCommand> logger)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var nameArg = new Argument<string>("name")
        {
            Description = "Unique name for the feed"
        };

        var urlArg = new Argument<string>("url")
        {
            Description = "NuGet v3 API URL or local directory path"
        };

        var command = new Command("add", "Add a NuGet feed");
        command.Arguments.Add(nameArg);
        command.Arguments.Add(urlArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArg);
            var url = parseResult.GetValue(urlArg);
            return await ExecuteAsync(name!, url!, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string name, string url, CancellationToken cancellationToken)
    {
        try
        {
            // Check for reserved names
            if (name.Equals("bundled", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]WARNING[/] 'bundled' is a reserved feed name");
                return 1;
            }

            if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]WARNING[/] 'nuget.org' is a reserved feed name");
                return 1;
            }

            LogAddingFeed(logger, name, url);

            await GlobalConfigManager.AddFeedAsync(name, url, cancellationToken);

            AnsiConsole.MarkupLine($"[green]✓[/] Added feed [cyan]{name}[/]");
            AnsiConsole.MarkupLine($"  URL: [dim]{url}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Config: [dim]{GlobalConfigManager.ConfigFilePath}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Run [cyan]revela packages refresh[/] to update the package index.[/]");

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]WARNING[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            LogAddFailed(logger, name, ex);
            ErrorPanels.ShowException(ex, "Failed to add feed.");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Adding feed {Name} with URL {Url}")]
    private static partial void LogAddingFeed(ILogger logger, string name, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add feed {Name}")]
    private static partial void LogAddFailed(ILogger logger, string name, Exception exception);
}
