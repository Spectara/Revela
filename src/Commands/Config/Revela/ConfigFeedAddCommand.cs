using System.CommandLine;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Revela;

/// <summary>
/// Command to add a NuGet feed
/// </summary>
public sealed partial class ConfigFeedAddCommand(
    ILogger<ConfigFeedAddCommand> logger)
{
    /// <summary>
    /// Creates the CLI command
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
            LogAddingFeed(logger, name, url);

            await GlobalConfigManager.AddFeedAsync(name, url, cancellationToken);

            AnsiConsole.MarkupLine($"[green]✓[/] Added feed [cyan]{name}[/]");
            AnsiConsole.MarkupLine($"  URL: [dim]{url}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Config: [dim]{GlobalConfigManager.ConfigFilePath}[/]");

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
