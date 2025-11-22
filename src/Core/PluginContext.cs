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
                foreach (var cmd in plugin.GetCommands())
                {
                    RegisterCommand(rootCommand, plugin, cmd);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to register commands for plugin '{plugin.Metadata.Name}': {ex.Message}");
            }
        }
    }

    private static void RegisterCommand(RootCommand rootCommand, IPlugin plugin, Command command)
    {
        if (!string.IsNullOrEmpty(plugin.Metadata.ParentCommand))
        {
            // Plugin wants a parent command
            var parentCmd = GetOrCreateParentCommand(rootCommand, plugin.Metadata.ParentCommand);

            if (parentCmd.Subcommands.Any(sc => sc.Name == command.Name))
            {
                Console.Error.WriteLine($"Warning: Plugin '{plugin.Metadata.Name}' tried to register duplicate command '{command.Name}' under '{plugin.Metadata.ParentCommand}'");
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

    private static Command GetOrCreateParentCommand(RootCommand root, string parentName)
    {
        var existing = root.Subcommands.FirstOrDefault(c => c.Name == parentName);
        if (existing is not null)
        {
            return existing;
        }

        // Create parent command based on name
        var description = parentName switch
        {
            "source" => "Manage image sources",
            "deploy" => "Deploy generated site",
            _ => $"{parentName} commands"
        };

        var parent = new Command(parentName, description);
        root.Subcommands.Add(parent);
        return parent;
    }

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin '{PluginName}' initialized successfully")]
    private static partial void LogPluginInitialized(ILogger logger, string pluginName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize plugin '{PluginName}'")]
    private static partial void LogPluginInitializationFailed(ILogger logger, string pluginName, Exception exception);
}
