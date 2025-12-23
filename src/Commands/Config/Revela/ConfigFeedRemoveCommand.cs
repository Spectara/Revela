using System.CommandLine;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Revela;

/// <summary>
/// Command to remove a NuGet feed
/// </summary>
public sealed partial class ConfigFeedRemoveCommand(
    ILogger<ConfigFeedRemoveCommand> logger)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var nameArg = new Argument<string>("name")
        {
            Description = "Name of the feed to remove"
        };

        var command = new Command("remove", "Remove a NuGet feed");
        command.Arguments.Add(nameArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArg);
            return await ExecuteAsync(name!, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            // Check for reserved name
            if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]WARNING[/] Cannot remove built-in 'nuget.org' feed");
                return 1;
            }

            LogRemovingFeed(logger, name);

            var removed = await GlobalConfigManager.RemoveFeedAsync(name, cancellationToken);

            if (removed)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Removed feed [cyan]{name}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"Config: [dim]{GlobalConfigManager.ConfigFilePath}[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]WARNING[/] Feed '{name}' not found");
                return 1;
            }
        }
        catch (Exception ex)
        {
            LogRemoveFailed(logger, name, ex);
            ErrorPanels.ShowException(ex, "Failed to remove feed.");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing feed {Name}")]
    private static partial void LogRemovingFeed(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to remove feed {Name}")]
    private static partial void LogRemoveFailed(ILogger logger, string name, Exception exception);
}
