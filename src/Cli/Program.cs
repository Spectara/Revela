using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core;
using Spectara.Revela.Features.Init;
using Spectara.Revela.Features.Plugins;

// Build root command
var rootCommand = new RootCommand("Revela - Modern static site generator for photographers");

// Add core commands (manual registration)
rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Subcommands.Add(PluginCommand.Create());
// rootCommand.Subcommands.Add(GenerateCommand.Create()); // TODO: Implement later
// rootCommand.Subcommands.Add(ServeCommand.Create());    // TODO: Implement later

// Setup basic DI for plugins
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var serviceProvider = services.BuildServiceProvider();

// Load and register plugin commands (dynamic)
try
{
    var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<PluginLoader>();
    var pluginLoader = new PluginLoader(logger);

    pluginLoader.LoadPlugins();
    var plugins = pluginLoader.GetLoadedPlugins();

    foreach (var plugin in plugins)
    {
        try
        {
            plugin.Initialize(serviceProvider);

            foreach (var cmd in plugin.GetCommands())
            {
                rootCommand.Subcommands.Add(cmd);
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

// Parse and execute
var parseResult = rootCommand.Parse(args);
return parseResult.Invoke();


