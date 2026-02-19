using System.CommandLine;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core;

/// <summary>
/// Internal implementation of <see cref="IPluginContext"/>.
/// </summary>
internal sealed class PluginContext(IReadOnlyList<ILoadedPluginInfo> plugins, ILogger<PluginContext> logger) : IPluginContext
{
    public IReadOnlyList<ILoadedPluginInfo> Plugins { get; } = plugins;

    public void RegisterCommands(RootCommand rootCommand, IServiceProvider services, Action<Command, int, string?, bool, bool>? onCommandRegistered = null)
    {
        foreach (var pluginInfo in Plugins)
        {
            try
            {
                foreach (var descriptor in pluginInfo.Plugin.GetCommands(services))
                {
                    RegisterCommand(rootCommand, pluginInfo.Plugin, descriptor, onCommandRegistered);
                }
            }
            catch (Exception ex)
            {
                logger.CommandRegistrationFailed(pluginInfo.Plugin.Metadata.Name, ex);
            }
        }
    }

    private void RegisterCommand(RootCommand rootCommand, IPlugin plugin, CommandDescriptor descriptor, Action<Command, int, string?, bool, bool>? onCommandRegistered)
    {
        var command = descriptor.Command;
        var parentPath = descriptor.ParentCommand;

        if (!string.IsNullOrEmpty(parentPath))
        {
            // Plugin wants a parent command (supports nested paths like "init source")
            var parentCmd = GetOrCreateParentCommand(rootCommand, parentPath, onCommandRegistered);

            if (parentCmd.Subcommands.Any(sc => sc.Name == command.Name))
            {
                logger.DuplicateSubcommand(plugin.Metadata.Name, command.Name, parentPath);
                return;
            }

            parentCmd.Subcommands.Add(command);
        }
        else
        {
            // No parent - register directly under root
            if (rootCommand.Subcommands.Any(sc => sc.Name == command.Name))
            {
                logger.DuplicateRootCommand(plugin.Metadata.Name, command.Name);
                return;
            }

            rootCommand.Subcommands.Add(command);
        }

        // Notify caller about registered command with its order, group, project requirement, and hide flag
        onCommandRegistered?.Invoke(command, descriptor.Order, descriptor.Group, descriptor.RequiresProject, descriptor.HideWhenProjectExists);
    }

    /// <summary>
    /// Gets or creates a parent command, supporting nested paths like "init source".
    /// </summary>
    private static Command GetOrCreateParentCommand(RootCommand root, string parentPath, Action<Command, int, string?, bool, bool>? onCommandRegistered)
    {
        // Split path into segments: "init source" → ["init", "source"]
        var segments = parentPath.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Command current = root;

        foreach (var segment in segments)
        {
            var existing = current.Subcommands.FirstOrDefault(c => c.Name == segment);
            if (existing is not null)
            {
                current = existing;
            }
            else
            {
                // Create new command with appropriate description, order, group, project requirement, and hide flag
                var (description, order, group, requiresProject, hideWhenProjectExists) = GetCommandInfo(segment);
                var newCommand = new Command(segment, description);
                current.Subcommands.Add(newCommand);
                onCommandRegistered?.Invoke(newCommand, order, group, requiresProject, hideWhenProjectExists);
                current = newCommand;
            }
        }

        return current;
    }

    private static (string Description, int Order, string? Group, bool RequiresProject, bool HideWhenProjectExists) GetCommandInfo(string commandName)
    {
        return commandName switch
        {
            // Parent commands that don't require project (setup/management)
            "init" => ("Initialize project, sources, or plugins", 30, "Setup", false, false),

            // Parent commands that require project (content operations)
            "source" => ("Image source providers", 20, "Content", true, false),
            "deploy" => ("Deploy generated site", 55, "Build", true, false),

            // Default: require project
            _ => ($"{commandName} commands", 50, null, true, false)
        };
    }
}
