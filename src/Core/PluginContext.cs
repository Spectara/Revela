using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
#pragma warning disable IDE0005 // Using directive is necessary for LoggerMessage attribute
using Microsoft.Extensions.Logging;
#pragma warning restore IDE0005
using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Core;

/// <summary>
/// Internal implementation of IPluginContext
/// </summary>
internal sealed partial class PluginContext(IReadOnlyList<IPlugin> plugins) : IPluginContext
{
    public IReadOnlyList<IPlugin> Plugins { get; } = plugins;

    public void Initialize(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<PluginContext>();

        foreach (var plugin in Plugins)
        {
            try
            {
                plugin.Initialize(serviceProvider);
                if (logger is not null)
                {
                    LogPluginInitialized(logger, plugin.Metadata.Name);
                }
            }
            catch (Exception ex)
            {
                if (logger is not null)
                {
                    LogPluginInitializationFailed(logger, plugin.Metadata.Name, ex);
                }
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
                Console.Error.WriteLine($"Failed to register commands for plugin '{plugin.Metadata.Name}': {ex.Message}");
            }
        }
    }

    private static void RegisterCommand(RootCommand rootCommand, IPlugin plugin, CommandDescriptor descriptor)
    {
        var command = descriptor.Command;
        var parentPath = descriptor.ParentCommand;

        if (!string.IsNullOrEmpty(parentPath))
        {
            // Plugin wants a parent command (supports nested paths like "init source")
            var parentCmd = GetOrCreateParentCommand(rootCommand, parentPath);

            if (parentCmd.Subcommands.Any(sc => sc.Name == command.Name))
            {
                Console.Error.WriteLine($"Warning: Plugin '{plugin.Metadata.Name}' tried to register duplicate command '{command.Name}' under '{parentPath}'");
                return;
            }

            parentCmd.Subcommands.Add(command);
        }
        else
        {
            // No parent - register directly under root
            if (rootCommand.Subcommands.Any(sc => sc.Name == command.Name))
            {
                Console.Error.WriteLine($"Warning: Plugin '{plugin.Metadata.Name}' tried to register duplicate root command '{command.Name}'");
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

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin '{PluginName}' initialized successfully")]
    private static partial void LogPluginInitialized(ILogger logger, string pluginName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize plugin '{PluginName}'")]
    private static partial void LogPluginInitializationFailed(ILogger logger, string pluginName, Exception exception);
}
