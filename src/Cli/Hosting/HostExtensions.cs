using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Spectara.Revela.Commands.Clean.Commands;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Create;
using Spectara.Revela.Commands.Generate.Commands;
using Spectara.Revela.Commands.Init;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Commands.Theme;
using Spectara.Revela.Sdk.Abstractions;

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

        // Get registries for interactive menu
        var groupRegistry = services.GetRequiredService<CommandGroupRegistry>();
        var orderRegistry = services.GetRequiredService<CommandOrderRegistry>();

        // Register well-known groups with display order
        // Build first (most used), then Content, Setup, Customize
        groupRegistry.Register(CommandGroups.Build, 10);
        groupRegistry.Register(CommandGroups.Content, 20);
        groupRegistry.Register(CommandGroups.Setup, 30);
        groupRegistry.Register(CommandGroups.Customize, 40);

        // Create root command
        var rootCommand = new RootCommand(description);

        // Core commands (resolved from DI) with display order and group assignment
        // Build group: generate (10), clean (20)
        var generateCommand = services.GetRequiredService<GenerateCommand>();
        var generateCmd = generateCommand.Create();
        rootCommand.Subcommands.Add(generateCmd);
        orderRegistry.Register(generateCmd, 10);
        orderRegistry.RegisterGroup(generateCmd, CommandGroups.Build);
        RegisterSubcommandOrders(generateCmd, orderRegistry);

        var cleanCommand = services.GetRequiredService<CleanCommand>();
        var cleanCmd = cleanCommand.Create();
        rootCommand.Subcommands.Add(cleanCmd);
        orderRegistry.Register(cleanCmd, 20);
        orderRegistry.RegisterGroup(cleanCmd, CommandGroups.Build);
        RegisterCleanSubcommandOrders(cleanCmd, orderRegistry);

        // Content group: create (10)
        var createCommand = services.GetRequiredService<CreateCommand>();
        var createCmd = createCommand.Create();
        rootCommand.Subcommands.Add(createCmd);
        orderRegistry.Register(createCmd, 10);
        orderRegistry.RegisterGroup(createCmd, CommandGroups.Content);

        // Setup group: init (10), config (20), restore (30) - workflow order
        var initCommand = services.GetRequiredService<InitCommand>();
        var initCmd = initCommand.Create();
        rootCommand.Subcommands.Add(initCmd);
        orderRegistry.Register(initCmd, 10);
        orderRegistry.RegisterGroup(initCmd, CommandGroups.Setup);
        // Note: RegisterInitSubcommandOrders called after plugins.RegisterCommands()

        var configCommand = services.GetRequiredService<ConfigCommand>();
        var configCmd = configCommand.Create();
        rootCommand.Subcommands.Add(configCmd);
        orderRegistry.Register(configCmd, 20);
        orderRegistry.RegisterGroup(configCmd, CommandGroups.Setup);
        // Note: RegisterConfigSubcommandOrders called after plugins.RegisterCommands()

        var restoreCommand = services.GetRequiredService<RestoreCommand>();
        var restoreCmd = restoreCommand.Create();
        rootCommand.Subcommands.Add(restoreCmd);
        orderRegistry.Register(restoreCmd, 30);
        orderRegistry.RegisterGroup(restoreCmd, CommandGroups.Setup);

        // Customize group: theme (10), plugin (20)
        var themeCommand = services.GetRequiredService<ThemeCommand>();
        var themeCmd = themeCommand.Create();
        rootCommand.Subcommands.Add(themeCmd);
        orderRegistry.Register(themeCmd, 10);
        orderRegistry.RegisterGroup(themeCmd, CommandGroups.Customize);
        RegisterThemeSubcommandOrders(themeCmd, orderRegistry);

        var pluginCommand = services.GetRequiredService<PluginCommand>();
        var pluginCmd = pluginCommand.Create();
        rootCommand.Subcommands.Add(pluginCmd);
        orderRegistry.Register(pluginCmd, 20);
        orderRegistry.RegisterGroup(pluginCmd, CommandGroups.Customize);
        RegisterPluginSubcommandOrders(pluginCmd, orderRegistry);

        // Plugin commands (with smart parent handling, order, and group registration)
        plugins.RegisterCommands(
            rootCommand,
            (cmd, order, group) =>
            {
                orderRegistry.Register(cmd, order);
                if (!string.IsNullOrEmpty(group))
                {
                    groupRegistry.GetOrCreate(group);
                    orderRegistry.RegisterGroup(cmd, group);
                }
            });

        // Register subcommand orders AFTER plugins have added their subcommands
        // This ensures plugin subcommands (like onedrive, serve under init) get proper ordering
        RegisterInitSubcommandOrders(initCmd, orderRegistry, groupRegistry);
        RegisterConfigSubcommandOrders(configCmd, orderRegistry);

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

    /// <summary>
    /// Registers order for subcommands of theme command.
    /// </summary>
    private static void RegisterThemeSubcommandOrders(Command themeCmd, CommandOrderRegistry orderRegistry)
    {
        // Order within theme: list (10), files (20), extract (30)
        foreach (var sub in themeCmd.Subcommands)
        {
            var order = sub.Name switch
            {
                "list" => 10,
                "files" => 20,
                "extract" => 30,
                _ => CommandOrderRegistry.DefaultOrder
            };
            orderRegistry.Register(sub, order);
        }
    }

    /// <summary>
    /// Registers order for subcommands of plugin command.
    /// </summary>
    private static void RegisterPluginSubcommandOrders(Command pluginCmd, CommandOrderRegistry orderRegistry)
    {
        // Order within plugin: list (10), install (20), uninstall (30)
        foreach (var sub in pluginCmd.Subcommands)
        {
            var order = sub.Name switch
            {
                "list" => 10,
                "install" => 20,
                "uninstall" => 30,
                _ => CommandOrderRegistry.DefaultOrder
            };
            orderRegistry.Register(sub, order);
        }
    }

    /// <summary>
    /// Registers order and groups for subcommands of init command.
    /// </summary>
    private static void RegisterInitSubcommandOrders(
        Command initCmd,
        CommandOrderRegistry orderRegistry,
        CommandGroupRegistry groupRegistry)
    {
        // Define init-specific subgroups
        const string project = "Project";
        const string plugins = "Plugins";

        // Register group orders (Project first, then Plugins)
        groupRegistry.Register(project, 10);
        groupRegistry.Register(plugins, 20);

        // Order within init: all (0), project (10), site (20) for Project group
        // Plugin configs get default order (50+)
        foreach (var sub in initCmd.Subcommands)
        {
            var (order, group) = sub.Name switch
            {
                "all" => (0, project),
                "project" => (10, project),
                "site" => (20, project),
                _ => (CommandOrderRegistry.DefaultOrder, plugins)
            };
            orderRegistry.Register(sub, order);
            orderRegistry.RegisterGroup(sub, group);
        }
    }

    /// <summary>
    /// Registers order for subcommands of config command.
    /// </summary>
    private static void RegisterConfigSubcommandOrders(Command configCmd, CommandOrderRegistry orderRegistry)
    {
        // Order within config: show (0), site (10), theme (20), image (30), feed (40), path (50)
        foreach (var sub in configCmd.Subcommands)
        {
            var order = sub.Name switch
            {
                "show" => 0,
                "site" => 10,
                "theme" => 20,
                "image" => 30,
                "feed" => 40,
                "path" => 50,
                _ => CommandOrderRegistry.DefaultOrder // Plugin config commands
            };
            orderRegistry.Register(sub, order);
        }
    }
}
