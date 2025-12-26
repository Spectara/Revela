using System.CommandLine;

namespace Spectara.Revela.Commands.Config.Feed;

/// <summary>
/// Parent command for NuGet feed management.
/// </summary>
public sealed class FeedCommand(
    ListCommand listCommand,
    AddCommand addCommand,
    RemoveCommand removeCommand)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("feed", "Manage NuGet feeds for package installation");

        command.Subcommands.Add(listCommand.Create());
        command.Subcommands.Add(addCommand.Create());
        command.Subcommands.Add(removeCommand.Create());

        return command;
    }
}
