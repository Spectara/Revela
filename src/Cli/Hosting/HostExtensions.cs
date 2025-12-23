using System.CommandLine;
using System.CommandLine.Help;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Spectara.Revela.Commands.Clean.Commands;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Create;
using Spectara.Revela.Commands.Generate.Commands;
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
        // Build first (most used), then Content, Setup, Addons
        groupRegistry.Register(CommandGroups.Build, 10);
        groupRegistry.Register(CommandGroups.Content, 20);
        groupRegistry.Register(CommandGroups.Setup, 30);
        groupRegistry.Register(CommandGroups.Addons, 40);

        // Create root command
        var rootCommand = new RootCommand(description);

        // Replace default HelpOption action with grouped help
        for (var i = 0; i < rootCommand.Options.Count; i++)
        {
            if (rootCommand.Options[i] is HelpOption helpOption)
            {
                var defaultHelpAction = (HelpAction)helpOption.Action!;
                helpOption.Action = new GroupedHelpAction(groupRegistry, orderRegistry, defaultHelpAction);
                break;
            }
        }

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

        // Setup group: config (10), restore (20) - workflow order
        var configCommand = services.GetRequiredService<ConfigCommand>();
        var configCmd = configCommand.Create();
        rootCommand.Subcommands.Add(configCmd);
        orderRegistry.Register(configCmd, 10);
        orderRegistry.RegisterGroup(configCmd, CommandGroups.Setup);
        // Note: RegisterConfigSubcommandOrders called after plugins.RegisterCommands()

        // Addons group: restore (5), theme (10), plugin (20)
        var restoreCommand = services.GetRequiredService<RestoreCommand>();
        var restoreCmd = restoreCommand.Create();
        rootCommand.Subcommands.Add(restoreCmd);
        orderRegistry.Register(restoreCmd, 5);
        orderRegistry.RegisterGroup(restoreCmd, CommandGroups.Addons);

        var themeCommand = services.GetRequiredService<ThemeCommand>();
        var themeCmd = themeCommand.Create();
        rootCommand.Subcommands.Add(themeCmd);
        orderRegistry.Register(themeCmd, 10);
        orderRegistry.RegisterGroup(themeCmd, CommandGroups.Addons);
        RegisterThemeSubcommandOrders(themeCmd, orderRegistry);

        var pluginCommand = services.GetRequiredService<PluginCommand>();
        var pluginCmd = pluginCommand.Create();
        rootCommand.Subcommands.Add(pluginCmd);
        orderRegistry.Register(pluginCmd, 20);
        orderRegistry.RegisterGroup(pluginCmd, CommandGroups.Addons);
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
        // This ensures plugin subcommands get proper ordering
        RegisterConfigSubcommandOrders(configCmd, orderRegistry, groupRegistry);

        // Replace default help with grouped help output
        ConfigureGroupedHelp(rootCommand, groupRegistry, orderRegistry);

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
    /// Configures the root command to use grouped help output.
    /// </summary>
    private static void ConfigureGroupedHelp(
        RootCommand rootCommand,
        CommandGroupRegistry groupRegistry,
        CommandOrderRegistry orderRegistry) =>
        // Recursively configure grouped help for all commands with subcommands
        ConfigureGroupedHelpRecursive(rootCommand, groupRegistry, orderRegistry);

    /// <summary>
    /// Recursively configures grouped help for a command and all its subcommands.
    /// </summary>
    private static void ConfigureGroupedHelpRecursive(
        Command command,
        CommandGroupRegistry groupRegistry,
        CommandOrderRegistry orderRegistry)
    {
        // Only configure if command has visible subcommands
        var hasSubcommands = command.Subcommands.Any(c => !c.Hidden);
        if (!hasSubcommands)
        {
            return;
        }

        // Find and replace the HelpOption action
        foreach (var option in command.Options)
        {
            if (option is HelpOption helpOption && helpOption.Action is HelpAction defaultHelp)
            {
                helpOption.Action = new GroupedHelpAction(groupRegistry, orderRegistry, defaultHelp);
                break;
            }
        }

        // Recurse into subcommands
        foreach (var sub in command.Subcommands)
        {
            ConfigureGroupedHelpRecursive(sub, groupRegistry, orderRegistry);
        }
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
    /// Registers order and groups for subcommands of config command.
    /// </summary>
    private static void RegisterConfigSubcommandOrders(
        Command configCmd,
        CommandOrderRegistry orderRegistry,
        CommandGroupRegistry groupRegistry)
    {
        // Register config-specific groups with display order
        groupRegistry.Register(CommandGroups.ConfigProject, 10);
        groupRegistry.Register(CommandGroups.ConfigSource, 20);
        groupRegistry.Register(CommandGroups.ConfigAddons, 30);

        foreach (var sub in configCmd.Subcommands)
        {
            // Assign order and group based on command name
            var (order, group) = sub.Name switch
            {
                // Project group: core project settings
                "project" => (10, CommandGroups.ConfigProject),
                "theme" => (20, CommandGroups.ConfigProject),
                "image" => (30, CommandGroups.ConfigProject),
                "site" => (40, CommandGroups.ConfigProject),

                // Addons group: optional plugin features
                "feed" => (10, CommandGroups.ConfigAddons),
                "serve" => (20, CommandGroups.ConfigAddons),
                "statistics" => (30, CommandGroups.ConfigAddons),

                // Ungrouped: locations (at the end)
                "locations" => (90, null),

                // Plugin commands use Group from CommandDescriptor
                _ => (CommandOrderRegistry.DefaultOrder, null)
            };

            orderRegistry.Register(sub, order);
            if (group is not null)
            {
                orderRegistry.RegisterGroup(sub, group);
            }
        }
    }
}
