using System.CommandLine;

namespace Spectara.Revela.Commands.Config.Revela;

/// <summary>
/// Parent command for NuGet feed management
/// </summary>
public sealed class ConfigFeedCommand(
    ConfigFeedListCommand listCommand,
    ConfigFeedAddCommand addCommand,
    ConfigFeedRemoveCommand removeCommand)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var command = new Command("feed", "Manage NuGet feeds for plugin installation");

        command.Subcommands.Add(listCommand.Create());
        command.Subcommands.Add(addCommand.Create());
        command.Subcommands.Add(removeCommand.Create());

        return command;
    }
}
