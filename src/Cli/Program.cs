using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Features.Init;
using Spectara.Revela.Features.Plugins;

// ✅ Create Host with minimal defaults
// ContentRoot = Working Directory (perfect for Global Tools!)
// This allows user configs (logging.json, onedrive.json, project.json) to be loaded from working directory
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    DisableDefaults = true  // We configure everything ourselves for full control
});

// ✅ Configure configuration sources (Working Directory)
builder.Configuration
    // Optional logging.json from working directory
    .AddJsonFile("logging.json", optional: true, reloadOnChange: true)
    // Environment variables (REVELA__*)
    .AddEnvironmentVariables(prefix: "REVELA__")
    // Command-line arguments
    .AddCommandLine(args);

// ✅ Configure logging from config OR defaults
var loggingConfig = new LoggingConfig();
builder.Configuration.GetSection(LoggingConfig.SectionName).Bind(loggingConfig);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Apply log levels from config
foreach (var (category, level) in loggingConfig.LogLevel)
{
    if (Enum.TryParse<LogLevel>(level, ignoreCase: true, out var logLevel))
    {
        if (category == "Default")
        {
            builder.Logging.SetMinimumLevel(logLevel);
        }
        else
        {
            builder.Logging.AddFilter(category, logLevel);
        }
    }
}

// ✅ Load and register plugins
// Plugins will register their config sources (e.g., onedrive.json) from working directory
var plugins = builder.Services.AddPlugins(builder.Configuration);

// ✅ Build host (creates ServiceProvider with all services)
var host = builder.Build();

// ✅ Initialize plugins with built ServiceProvider
plugins.Initialize(host.Services);

// Build root command
var rootCommand = new RootCommand("Revela - Modern static site generator for photographers");

// Add core commands
rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Subcommands.Add(PluginCommand.Create());
// TODO: Add GenerateCommand when implemented
// TODO: Add ServeCommand when implemented

// ✅ Register plugin commands (with smart parent handling)
plugins.RegisterCommands(rootCommand);

// Parse and execute
return rootCommand.Parse(args).Invoke();


