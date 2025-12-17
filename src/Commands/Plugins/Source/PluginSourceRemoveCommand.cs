using System.CommandLine;
using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins.Source;

/// <summary>
/// Command to remove a NuGet source
/// </summary>
public sealed partial class PluginSourceRemoveCommand(
    ILogger<PluginSourceRemoveCommand> logger)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Name of the source to remove"
        };

        var command = new Command("remove", "Remove a NuGet package source");
        command.Arguments.Add(nameArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            return await ExecuteAsync(name, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            LogRemovingSource(logger, name);
            var removed = await NuGetSourceManager.RemoveSourceAsync(name, cancellationToken);

            if (removed)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Removed source [cyan]{name}[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]![/] Source [cyan]{name}[/] not found");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            LogRemoveFailed(logger, ex, name);
            AnsiConsole.MarkupLine($"[red]ERROR[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            LogRemoveFailed(logger, ex, name);
            AnsiConsole.MarkupLine($"[red]ERROR[/] Failed to remove source: {ex.Message}");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing NuGet source: {name}")]
    private static partial void LogRemovingSource(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to remove source: {name}")]
    private static partial void LogRemoveFailed(ILogger logger, Exception exception, string name);
}
