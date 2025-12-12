using System.CommandLine;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Logging;

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

    public void RegisterCommands(RootCommand rootCommand)
    {
        foreach (var plugin in Plugins)
        {
            try
            {
                foreach (var descriptor in plugin.GetCommands())
                {
                    RegisterCommand(rootCommand, plugin, descriptor);
                }
            }
            catch (Exception ex)
            {
                logger.CommandRegistrationFailed(plugin.Metadata.Name, ex);
            }
        }
    }

    private void RegisterCommand(RootCommand rootCommand, IPlugin plugin, CommandDescriptor descriptor)
    {
        var command = descriptor.Command;
        var parentPath = descriptor.ParentCommand;

        if (!string.IsNullOrEmpty(parentPath))
        {
            // Plugin wants a parent command (supports nested paths like "init source")
            var parentCmd = GetOrCreateParentCommand(rootCommand, parentPath);

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
    }

    /// <summary>
    /// Gets or creates a parent command, supporting nested paths like "init source".
    /// </summary>
    private static Command GetOrCreateParentCommand(RootCommand root, string parentPath)
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
                // Create new command with appropriate description
                var description = GetCommandDescription(segment);
                var newCommand = new Command(segment, description);
                current.Subcommands.Add(newCommand);
                current = newCommand;
            }
        }

        return current;
    }

    private static string GetCommandDescription(string commandName)
    {
        return commandName switch
        {
            "init" => "Initialize project, sources, or plugins",
            "source" => "Image source providers",
            "deploy" => "Deploy generated site",
            _ => $"{commandName} commands"
        };
    }
}

