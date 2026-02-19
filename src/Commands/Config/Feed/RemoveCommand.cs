using System.CommandLine;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Feed;

/// <summary>
/// Command to remove a NuGet feed.
/// </summary>
internal sealed partial class RemoveCommand(
    ILogger<RemoveCommand> logger,
    INuGetSourceManager nugetSourceManager)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var nameArg = new Argument<string?>("name")
        {
            Description = "Name of the feed to remove",
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("remove", "Remove a NuGet feed");
        command.Arguments.Add(nameArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArg);
            return await ExecuteAsync(name, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string? name, CancellationToken cancellationToken)
    {
        try
        {
            // If no name provided, show interactive selection
            if (string.IsNullOrEmpty(name))
            {
                name = await SelectFeedInteractivelyAsync(cancellationToken);
                if (name is null)
                {
                    return 0; // User cancelled
                }
            }

            // Check for reserved names
            if (name.Equals("bundled", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]WARNING[/] Cannot remove built-in 'bundled' feed");
                return 1;
            }

            if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]WARNING[/] Cannot remove built-in 'nuget.org' feed");
                return 1;
            }

            LogRemovingFeed(logger, name);

            var removed = await GlobalConfigManager.RemoveFeedAsync(name, cancellationToken);

            if (removed)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} Removed feed [cyan]{name}[/]");
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
            LogRemoveFailed(logger, name ?? "unknown", ex);
            ErrorPanels.ShowException(ex, "Failed to remove feed.");
            return 1;
        }
    }

    private async Task<string?> SelectFeedInteractivelyAsync(CancellationToken cancellationToken)
    {
        var sources = await nugetSourceManager.GetAllSourcesWithLocationAsync(cancellationToken);

        // Filter out built-in feeds that can't be removed
        var removableFeeds = sources
            .Where(s => s.Location is not "bundled" and not "built-in")
            .Select(s => s.Source.Name)
            .ToList();

        if (removableFeeds.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No custom feeds configured[/]");
            AnsiConsole.MarkupLine("[dim]Use 'revela config feed add' to add a feed first[/]");
            return null;
        }

        // Add cancel option
        removableFeeds.Add("[dim]Cancel[/]");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select feed to remove:")
                .PageSize(10)
                .AddChoices(removableFeeds));

        if (selected == "[dim]Cancel[/]")
        {
            return null;
        }

        return selected;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing feed {Name}")]
    private static partial void LogRemovingFeed(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to remove feed {Name}")]
    private static partial void LogRemoveFailed(ILogger logger, string name, Exception exception);
}
