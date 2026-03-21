using System.CommandLine;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core;

/// <summary>
/// Internal implementation of <see cref="IPluginContext"/>.
/// </summary>
internal sealed class PluginContext(IReadOnlyList<LoadedPluginInfo> plugins, ILogger<PluginContext> logger) : IPluginContext
{
    public IReadOnlyList<LoadedPluginInfo> Plugins { get; } = plugins;

    public void RegisterCommands(RootCommand rootCommand, IServiceProvider services, CommandRegisteredCallback? onCommandRegistered = null)
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

    private void RegisterCommand(RootCommand rootCommand, IPlugin plugin, CommandDescriptor descriptor, CommandRegisteredCallback? onCommandRegistered)
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
            var existing = rootCommand.Subcommands.FirstOrDefault(sc => sc.Name == command.Name);
            if (existing is not null)
            {
                // An auto-created parent may already exist (created by another plugin's
                // ParentCommand reference). Merge the plugin's subcommands into it so
                // the real command's subcommands aren't lost.
                MergeSubcommands(existing, command);

                // Update metadata on the existing command (the auto-created parent had
                // default group/order; the plugin's descriptor has the real values)
                onCommandRegistered?.Invoke(existing, descriptor.Order, descriptor.Group, descriptor.RequiresProject, descriptor.HideWhenProjectExists, descriptor.IsSequentialStep);
                return;
            }
            else
            {
                rootCommand.Subcommands.Add(command);
            }
        }

        // Notify caller about registered command with its metadata
        onCommandRegistered?.Invoke(command, descriptor.Order, descriptor.Group, descriptor.RequiresProject, descriptor.HideWhenProjectExists, descriptor.IsSequentialStep);
    }

    /// <summary>
    /// Merges subcommands from a plugin's command into an existing auto-created parent.
    /// </summary>
    /// <remarks>
    /// When plugin A registers <c>ParentCommand: "generate"</c> before plugin B registers
    /// its own <c>generate</c> root command, an empty parent is auto-created. This method
    /// transfers plugin B's subcommands into the existing parent without losing plugin A's
    /// subcommands that were already added.
    /// </remarks>
    private static void MergeSubcommands(Command existing, Command incoming)
    {
        // Take the real description from the plugin's command (auto-created parents
        // have generic descriptions like "generate commands")
        if (!string.IsNullOrEmpty(incoming.Description))
        {
            existing.Description = incoming.Description;
        }

        foreach (var sub in incoming.Subcommands)
        {
            if (!existing.Subcommands.Any(s => s.Name == sub.Name))
            {
                existing.Subcommands.Add(sub);
            }
        }
    }

    /// <summary>
    /// Gets or creates a parent command, supporting nested paths like "init source".
    /// </summary>
    private static Command GetOrCreateParentCommand(RootCommand root, string parentPath, CommandRegisteredCallback? onCommandRegistered)
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
                onCommandRegistered?.Invoke(newCommand, order, group, requiresProject, hideWhenProjectExists, isSequentialStep: false);
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
