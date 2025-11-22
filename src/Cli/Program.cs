using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Features.Init;
using Spectara.Revela.Features.Plugins;

// ✅ Use Host.CreateApplicationBuilder for full .NET hosting features
// - Configuration: appsettings.json, environment variables, user secrets
// - Logging: Configuration-driven logging levels
// - Dependency Injection: Full DI container with all features
// - Environment: Development/Production/Staging support
var builder = Host.CreateApplicationBuilder(args);

// ✅ Load and register plugins
// Plugins will:
// 1. Register their config sources (e.g., onedrive.json)
// 2. Register their services (e.g., HttpClient, Commands, IOptions)
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


