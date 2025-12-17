using System.CommandLine;
using Spectara.Revela.Commands.Plugins.Source;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Parent command for NuGet source management
/// </summary>
public sealed class PluginSourceCommand(
    PluginSourceListCommand listCommand,
    PluginSourceAddCommand addCommand,
    PluginSourceRemoveCommand removeCommand)
{
    /// <summary>
    /// Creates the 'source' command with subcommands
    /// </summary>
    public Command Create()
    {
        var command = new Command("source", "Manage NuGet package sources");

        command.Subcommands.Add(listCommand.Create());
        command.Subcommands.Add(addCommand.Create());
        command.Subcommands.Add(removeCommand.Create());

        return command;
    }
}
