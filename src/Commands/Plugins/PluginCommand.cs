using System.CommandLine;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Parent command for plugin management.
/// </summary>
public sealed class PluginCommand(
    PluginListCommand listCommand,
    PluginInstallCommand installCommand,
    PluginUninstallCommand uninstallCommand)
{
    /// <summary>
    /// Creates the 'plugin' command with subcommands.
    /// </summary>
    public Command Create()
    {
        var command = new Command("plugin", "Manage Revela plugins");

        // Add subcommands (injected via DI)
        command.Subcommands.Add(listCommand.Create());
        command.Subcommands.Add(installCommand.Create());
        command.Subcommands.Add(uninstallCommand.Create());

        return command;
    }
}
