using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Spectara.Revela.Commands.Clean.Commands;
using Spectara.Revela.Commands.Generate.Commands;
using Spectara.Revela.Commands.Init;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Commands.Theme;
using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Cli.Hosting;

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
    /// 1. Creates the RootCommand with interactive mode handler
    /// 2. Initializes plugins via IPluginContext
    /// 3. Resolves and registers all core commands from DI
    /// 4. Registers plugin commands
    ///
    /// When invoked without arguments, the CLI enters interactive mode.
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

        // Get order registry for interactive menu sorting
        var orderRegistry = services.GetRequiredService<CommandOrderRegistry>();

        // Create root command
        var rootCommand = new RootCommand(description);

        // Core commands (resolved from DI) with display order
        // Order: generate (10), source (20-plugin), init (30), theme (40), plugin (50), restore (60), clean (70)
        var generateCommand = services.GetRequiredService<GenerateCommand>();
        var generateCmd = generateCommand.Create();
        rootCommand.Subcommands.Add(generateCmd);
        orderRegistry.Register(generateCmd, 10);
        RegisterSubcommandOrders(generateCmd, orderRegistry);

        var initCommand = services.GetRequiredService<InitCommand>();
        var initCmd = initCommand.Create();
        rootCommand.Subcommands.Add(initCmd);
        orderRegistry.Register(initCmd, 30);

        var themeCommand = services.GetRequiredService<ThemeCommand>();
        var themeCmd = themeCommand.Create();
        rootCommand.Subcommands.Add(themeCmd);
        orderRegistry.Register(themeCmd, 40);

        var pluginCommand = services.GetRequiredService<PluginCommand>();
        var pluginCmd = pluginCommand.Create();
        rootCommand.Subcommands.Add(pluginCmd);
        orderRegistry.Register(pluginCmd, 50);

        var restoreCommand = services.GetRequiredService<RestoreCommand>();
        var restoreCmd = restoreCommand.Create();
        rootCommand.Subcommands.Add(restoreCmd);
        orderRegistry.Register(restoreCmd, 60);

        var cleanCommand = services.GetRequiredService<CleanCommand>();
        var cleanCmd = cleanCommand.Create();
        rootCommand.Subcommands.Add(cleanCmd);
        orderRegistry.Register(cleanCmd, 70);
        RegisterCleanSubcommandOrders(cleanCmd, orderRegistry);

        // Plugin commands (with smart parent handling and order registration)
        plugins.RegisterCommands(rootCommand, (cmd, order) => orderRegistry.Register(cmd, order));

        // Set interactive mode handler for root command (no subcommand specified)
        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            // If we get here, no subcommand was specified - enter interactive mode
            var interactiveService = services.GetRequiredService<IInteractiveMenuService>();
            interactiveService.RootCommand = rootCommand;
            return await interactiveService.RunAsync(cancellationToken);
        });

        return rootCommand;
    }

    /// <summary>
    /// Registers order for subcommands of generate command.
    /// </summary>
    private static void RegisterSubcommandOrders(Command generateCmd, CommandOrderRegistry orderRegistry)
    {
        // Order within generate: all (0), scan (10), statistics (20-plugin), pages (30), images (40)
        foreach (var sub in generateCmd.Subcommands)
        {
            var order = sub.Name switch
            {
                "all" => AllCommand.Order,
                "scan" => 10,
                "pages" => 30,
                "images" => 40,
                _ => CommandOrderRegistry.DefaultOrder
            };
            orderRegistry.Register(sub, order);
        }
    }

    /// <summary>
    /// Registers order for subcommands of clean command.
    /// </summary>
    private static void RegisterCleanSubcommandOrders(Command cleanCmd, CommandOrderRegistry orderRegistry)
    {
        // Order within clean: all (0), output (10), cache (20), statistics (30-plugin)
        foreach (var sub in cleanCmd.Subcommands)
        {
            var order = sub.Name switch
            {
                "all" => CleanAllCommand.Order,
                "output" => CleanOutputCommand.Order,
                "cache" => CleanCacheCommand.Order,
                _ => CommandOrderRegistry.DefaultOrder
            };
            orderRegistry.Register(sub, order);
        }
    }
}
