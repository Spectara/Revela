using System.CommandLine;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core;

/// <summary>
/// Internal implementation of IPluginContext
/// </summary>
internal sealed class PluginContext(IReadOnlyList<IPlugin> plugins, ILogger<PluginContext> logger) : IPluginContext
{
    public IReadOnlyList<IPlugin> Plugins { get; } = plugins;

    public void Initialize(IServiceProvider serviceProvider)
    {
        foreach (var plugin in Plugins)
        {
            try
            {
                plugin.Initialize(serviceProvider);
                logger.PluginInitialized(plugin.Metadata.Name);
            }
            catch (Exception ex)
            {
                logger.PluginInitializationFailed(plugin.Metadata.Name, ex);
            }
        }
    }

    public void RegisterCommands(RootCommand rootCommand, Action<Command, int>? onCommandRegistered = null)
    {
        foreach (var plugin in Plugins)
        {
            try
            {
                foreach (var descriptor in plugin.GetCommands())
                {
                    RegisterCommand(rootCommand, plugin, descriptor, onCommandRegistered);
                }
            }
            catch (Exception ex)
            {
                logger.CommandRegistrationFailed(plugin.Metadata.Name, ex);
            }
        }
    }

    private void RegisterCommand(RootCommand rootCommand, IPlugin plugin, CommandDescriptor descriptor, Action<Command, int>? onCommandRegistered)
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

        // Notify caller about registered command with its order
        onCommandRegistered?.Invoke(command, descriptor.Order);
    }

    /// <summary>
    /// Gets or creates a parent command, supporting nested paths like "init source".
    /// </summary>
    private static Command GetOrCreateParentCommand(RootCommand root, string parentPath, Action<Command, int>? onCommandRegistered)
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
                // Create new command with appropriate description and order
                var (description, order) = GetCommandInfo(segment);
                var newCommand = new Command(segment, description);
                current.Subcommands.Add(newCommand);
                onCommandRegistered?.Invoke(newCommand, order);
                current = newCommand;
            }
        }

        return current;
    }

    private static (string Description, int Order) GetCommandInfo(string commandName)
    {
        return commandName switch
        {
            "init" => ("Initialize project, sources, or plugins", 30),
            "source" => ("Image source providers", 20),
            "deploy" => ("Deploy generated site", 55),
            _ => ($"{commandName} commands", 50)  // Default order
        };
    }
}

