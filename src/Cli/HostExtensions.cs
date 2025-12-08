using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Commands.Generate;
using Spectara.Revela.Commands.Init;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Commands.Theme;
using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Cli;

/// <summary>
/// Extension methods for activating Revela commands on an IHost.
/// </summary>
internal static class HostExtensions
{
    /// <summary>
    /// Creates and configures the Revela CLI root command.
    /// </summary>
    /// <remarks>
    /// This is the post-build phase that:
    /// 1. Creates the RootCommand
    /// 2. Initializes plugins via IPluginContext
    /// 3. Resolves and registers all core commands from DI
    /// 4. Registers plugin commands
    ///
    /// Call AddRevelaCommands() and AddPlugins() on the service collection first.
    ///
    /// Example:
    /// <code>
    /// var host = builder.Build();
    /// return host.UseRevelaCommands().Parse(args).Invoke();
    /// </code>
    /// </remarks>
    /// <param name="host">The built host with all services registered.</param>
    /// <param name="description">Optional custom description for the root command.</param>
    /// <returns>The configured root command ready for parsing and execution.</returns>
    public static RootCommand UseRevelaCommands(
        this IHost host,
        string description = "Revela - Modern static site generator for photographers")
    {
        var services = host.Services;

        // Get plugin context from DI and initialize
        var plugins = services.GetRequiredService<IPluginContext>();
        plugins.Initialize(services);

        // Create root command
        var rootCommand = new RootCommand(description);

        // Core commands (resolved from DI)
        var initCommand = services.GetRequiredService<InitCommand>();
        rootCommand.Subcommands.Add(initCommand.Create());

        var pluginCommand = services.GetRequiredService<PluginCommand>();
        rootCommand.Subcommands.Add(pluginCommand.Create());

        var generateCommand = services.GetRequiredService<GenerateCommand>();
        rootCommand.Subcommands.Add(generateCommand.Create());

        var restoreCommand = services.GetRequiredService<RestoreCommand>();
        rootCommand.Subcommands.Add(restoreCommand.Create());

        var themeCommand = services.GetRequiredService<ThemeCommand>();
        rootCommand.Subcommands.Add(themeCommand.Create());

        // Plugin commands (with smart parent handling)
        plugins.RegisterCommands(rootCommand);

        return rootCommand;
    }
}
