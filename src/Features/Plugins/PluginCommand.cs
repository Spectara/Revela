using System.CommandLine;

namespace Spectara.Revela.Features.Plugins;

/// <summary>
/// Parent command for plugin management
/// </summary>
public static class PluginCommand
{
    /// <summary>
    /// Creates the 'plugin' command with subcommands
    /// </summary>
    public static Command Create()
    {
        var command = new Command("plugin", "Manage Revela plugins");

        // Add subcommands
        command.Subcommands.Add(PluginListCommand.Create());
        command.Subcommands.Add(PluginInstallCommand.Create());
        command.Subcommands.Add(PluginUninstallCommand.Create());

        return command;
    }
}

