using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core;
using Spectara.Revela.Features.Init;
using Spectara.Revela.Features.Plugins;

// Setup basic DI
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// IMPORTANT: Load plugins BEFORE building ServiceProvider
// This allows plugins to register their own services
using var tempLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var tempLogger = tempLoggerFactory.CreateLogger<PluginLoader>();
var pluginLoader = new PluginLoader(tempLogger);
pluginLoader.LoadPlugins();
var plugins = pluginLoader.GetLoadedPlugins();

#if DEBUG
// Development: Load OneDrive plugin directly (not via plugin loader)
var oneDrivePlugin = new Spectara.Revela.Plugin.Source.OneDrive.OneDrivePlugin();
plugins = [oneDrivePlugin, .. plugins];
#endif

// Let plugins register their services BEFORE building ServiceProvider
foreach (var plugin in plugins)
{
    try
    {
        plugin.ConfigureServices(services);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Plugin '{plugin.Metadata.Name}' failed to configure services: {ex.Message}");
    }
}

// NOW build ServiceProvider with all plugin services registered
var serviceProvider = services.BuildServiceProvider();

// Build root command
var rootCommand = new RootCommand("Revela - Modern static site generator for photographers");

// Add core commands (manual registration)
rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Subcommands.Add(PluginCommand.Create());
// rootCommand.Subcommands.Add(GenerateCommand.Create()); // TODO: Implement later
// rootCommand.Subcommands.Add(ServeCommand.Create());    // TODO: Implement later

// Initialize plugins and register commands
try
{
    var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<PluginLoader>();

    foreach (var plugin in plugins)
    {
        try
        {
            plugin.Initialize(serviceProvider);

            foreach (var cmd in plugin.GetCommands())
            {
                // Smart parent command handling
                if (!string.IsNullOrEmpty(plugin.Metadata.ParentCommand))
                {
                    // Plugin wants a parent command - get or create it
                    var parentCmd = GetOrCreateParentCommand(rootCommand, plugin.Metadata.ParentCommand);

                    // Check for duplicate subcommand
                    if (parentCmd.Subcommands.Any(sc => sc.Name == cmd.Name))
                    {
                        Console.Error.WriteLine($"Warning: Plugin '{plugin.Metadata.Name}' tried to register duplicate command '{cmd.Name}' under '{plugin.Metadata.ParentCommand}'");
                        continue;
                    }

                    parentCmd.Subcommands.Add(cmd);
                }
                else
                {
                    // No parent - register directly under root
                    if (rootCommand.Subcommands.Any(sc => sc.Name == cmd.Name))
                    {
                        Console.Error.WriteLine($"Warning: Plugin '{plugin.Metadata.Name}' tried to register duplicate root command '{cmd.Name}'");
                        continue;
                    }

                    rootCommand.Subcommands.Add(cmd);
                }
            }
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
            logger.LogError(ex, "Failed to initialize plugin {PluginName}", plugin.Metadata.Name);
#pragma warning restore CA1848
        }
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Plugin loading failed: {ex.Message}");
    // Continue without plugins - core commands still work
}

// Helper to get or create parent commands
static Command GetOrCreateParentCommand(RootCommand root, string parentName)
{
    var existing = root.Subcommands.FirstOrDefault(c => c.Name == parentName);
    if (existing != null)
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

// Parse and execute
var parseResult = rootCommand.Parse(args);
return parseResult.Invoke();


