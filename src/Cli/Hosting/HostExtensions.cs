using System.CommandLine;
using System.CommandLine.Help;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Spectara.Revela.Commands.Clean.Commands;
using Spectara.Revela.Commands.Generate.Commands;
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

        // Unified command registration callback
        void OnCommandRegistered(Command cmd, int order, string? group, bool requiresProject, bool hideWhenProjectExists)
        {
            orderRegistry.Register(cmd, order);
            if (!string.IsNullOrEmpty(group))
            {
                groupRegistry.GetOrCreate(group);
                orderRegistry.RegisterGroup(cmd, group);
            }

            if (!requiresProject)
            {
                orderRegistry.RegisterNoProjectRequired(cmd);
            }

            if (hideWhenProjectExists)
            {
                orderRegistry.RegisterHideWhenProjectExists(cmd);
            }
        }

        // Core commands (via CoreCommandProvider, same pattern as plugins)
        var coreCommands = new CoreCommandProvider(services);
        foreach (var descriptor in coreCommands.GetCommands())
        {
            rootCommand.Subcommands.Add(descriptor.Command);
            OnCommandRegistered(
                descriptor.Command,
                descriptor.Order,
                descriptor.Group,
                descriptor.RequiresProject,
                descriptor.HideWhenProjectExists);

            // Register subcommand orders for commands with special handling
            RegisterSubcommandOrders(descriptor.Command, orderRegistry);
        }

        // Plugin commands (same callback for unified handling)
        plugins.RegisterCommands(rootCommand, OnCommandRegistered);

        // Register config subcommand orders AFTER plugins have added their subcommands
        var configCmd = rootCommand.Subcommands.FirstOrDefault(c => string.Equals(c.Name, "config", StringComparison.Ordinal));
        if (configCmd is not null)
        {
            RegisterConfigSubcommandOrders(configCmd, orderRegistry, groupRegistry);
        }

        // Replace default help with grouped help output for all commands
        ConfigureGroupedHelpRecursive(rootCommand, groupRegistry, orderRegistry);

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
    /// Subcommand order definitions for each parent command.
    /// Maps parent command name → (subcommand name → order).
    /// Unknown subcommands (e.g., from plugins) get <see cref="CommandOrderRegistry.DefaultOrder"/>.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, int>> SubcommandOrders = new(StringComparer.Ordinal)
    {
        // Order within generate: all (0), scan (10), statistics (20-plugin), pages (30), images (40)
        ["generate"] = new(StringComparer.Ordinal)
        {
            ["all"] = AllCommand.Order,
            ["scan"] = 10,
            ["pages"] = 30,
            ["images"] = 40,
        },
        // Order within clean: all (0), output (10), images (15), cache (20), statistics (30-plugin)
        ["clean"] = new(StringComparer.Ordinal)
        {
            ["all"] = CleanAllCommand.Order,
            ["output"] = CleanOutputCommand.Order,
            ["images"] = CleanImagesCommand.Order,
            ["cache"] = CleanCacheCommand.Order,
        },
        // Order within theme: list (10), files (20), extract (30)
        ["theme"] = new(StringComparer.Ordinal)
        {
            ["list"] = 10,
            ["files"] = 20,
            ["extract"] = 30,
        },
        // Order within plugin: list (10), install (20), uninstall (30)
        ["plugin"] = new(StringComparer.Ordinal)
        {
            ["list"] = 10,
            ["install"] = 20,
            ["uninstall"] = 30,
        },
        // Order within packages: refresh (10), search (20)
        ["packages"] = new(StringComparer.Ordinal)
        {
            ["refresh"] = 10,
            ["search"] = 20,
        },
    };

    /// <summary>
    /// Registers order for subcommands based on command name using the <see cref="SubcommandOrders"/> lookup.
    /// </summary>
    private static void RegisterSubcommandOrders(Command command, CommandOrderRegistry orderRegistry)
    {
        if (!SubcommandOrders.TryGetValue(command.Name, out var orderLookup))
        {
            return;
        }

        foreach (var sub in command.Subcommands)
        {
            var order = orderLookup.TryGetValue(sub.Name, out var value) ? value : CommandOrderRegistry.DefaultOrder;
            orderRegistry.Register(sub, order);
        }
    }

    /// <summary>
    /// Config subcommand metadata: order, group, and project requirement.
    /// Commands not listed here default to (DefaultOrder, null group, requiresProject: true).
    /// </summary>
    private static readonly Dictionary<string, (int Order, string? Group, bool RequiresProject)> ConfigSubcommandMeta =
        new(StringComparer.Ordinal)
        {
            // Project group: core project settings
            // "project" doesn't require project - it creates project.json
            ["project"] = (10, CommandGroups.ConfigProject, false),
            ["paths"] = (20, CommandGroups.ConfigProject, true),
            ["theme"] = (30, CommandGroups.ConfigProject, true),
            ["image"] = (40, CommandGroups.ConfigProject, true),
            ["site"] = (50, CommandGroups.ConfigProject, true),
            ["sorting"] = (60, CommandGroups.ConfigProject, true),

            // Packages group: feed management (global, no project needed)
            ["feed"] = (10, CommandGroups.ConfigPackages, false),

            // Addons group: optional plugin features (require project)
            ["serve"] = (10, CommandGroups.ConfigAddons, true),
            ["statistics"] = (20, CommandGroups.ConfigAddons, true),

            // Ungrouped: locations (global, no project needed)
            ["locations"] = (90, null, false),
        };

    /// <summary>
    /// Registers order, groups, and project requirements for config subcommands.
    /// </summary>
    private static void RegisterConfigSubcommandOrders(
        Command configCmd,
        CommandOrderRegistry orderRegistry,
        CommandGroupRegistry groupRegistry)
    {
        // Register config-specific groups with display order
        groupRegistry.Register(CommandGroups.ConfigProject, 10);
        groupRegistry.Register(CommandGroups.ConfigSource, 20);
        groupRegistry.Register(CommandGroups.ConfigPackages, 30);
        groupRegistry.Register(CommandGroups.ConfigAddons, 40);

        foreach (var sub in configCmd.Subcommands)
        {
            var (order, group, requiresProject) = ConfigSubcommandMeta.TryGetValue(sub.Name, out var meta)
                ? meta
                : (CommandOrderRegistry.DefaultOrder, null, true);

            orderRegistry.Register(sub, order);
            if (group is not null)
            {
                orderRegistry.RegisterGroup(sub, group);
            }

            if (!requiresProject)
            {
                orderRegistry.RegisterNoProjectRequired(sub);
            }
        }
    }
}
