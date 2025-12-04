using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Init;
using Spectara.Revela.Features.Plugins;
using Spectara.Revela.Features.Restore;
using Spectara.Revela.Features.Theme;

// ✅ Use Host.CreateApplicationBuilder for full .NET hosting features
// - Configuration: appsettings.json, environment variables, user secrets
// - Logging: Configuration-driven logging levels
// - Dependency Injection: Full DI container with all features
// - Environment: Development/Production/Staging support
var builder = Host.CreateApplicationBuilder(args);

// Load logging.json from working directory (optional, for user-specific logging config)
builder.Configuration.AddJsonFile(
    Path.Combine(Directory.GetCurrentDirectory(), "logging.json"),
    optional: true,
    reloadOnChange: true
);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// ✅ Register feature services (Vertical Slice Architecture)
builder.Services.AddGenerateFeature();
builder.Services.AddInitFeature();
builder.Services.AddPluginsFeature();
builder.Services.AddRestoreFeature();
builder.Services.AddThemeFeature();

// ✅ Load and register plugins
// Plugins will:
// 1. Framework auto-loads plugins/*.json and SPECTARA__REVELA__* env vars
// 2. Register their services (e.g., HttpClient, Commands, IOptions)
var plugins = builder.Services.AddPlugins(builder.Configuration);

// ✅ Build host (creates ServiceProvider with all services)
var host = builder.Build();

// ✅ Initialize plugins with built ServiceProvider
plugins.Initialize(host.Services);

// Build root command
var rootCommand = new RootCommand("Revela - Modern static site generator for photographers");

// ✅ Add core commands (all resolved from DI for Vertical Slice Architecture)
var initCommand = host.Services.GetRequiredService<InitCommand>();
rootCommand.Subcommands.Add(initCommand.Create());

var pluginCommand = host.Services.GetRequiredService<PluginCommand>();
rootCommand.Subcommands.Add(pluginCommand.Create());

var generateCommand = host.Services.GetRequiredService<GenerateCommand>();
rootCommand.Subcommands.Add(generateCommand.Create());

// ✅ Add restore command
var restoreCommand = host.Services.GetRequiredService<RestoreCommand>();
rootCommand.Subcommands.Add(restoreCommand.Create());

// ✅ Add theme command
rootCommand.Subcommands.Add(ThemeCommand.Create(host.Services));

// ✅ Register plugin commands (with smart parent handling)
plugins.RegisterCommands(rootCommand);

// Parse and execute
return rootCommand.Parse(args).Invoke();
