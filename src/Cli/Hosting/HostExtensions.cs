using System.Collections.Frozen;
using System.CommandLine;
using System.CommandLine.Help;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        // Get plugin context from DI
        var plugins = services.GetRequiredService<IPluginContext>();

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
        void OnCommandRegistered(Command cmd, CommandDescriptor desc)
        {
            orderRegistry.Register(cmd, desc.Order);
            if (!string.IsNullOrEmpty(desc.Group))
            {
                groupRegistry.GetOrCreate(desc.Group);
                orderRegistry.RegisterGroup(cmd, desc.Group);
            }

            if (!desc.RequiresProject)
            {
                orderRegistry.RegisterNoProjectRequired(cmd);
            }

            if (desc.HideWhenProjectExists)
            {
                orderRegistry.RegisterHideWhenProjectExists(cmd);
            }

            if (desc.IsSequentialStep)
            {
                orderRegistry.RegisterPipelineStep(cmd);
            }
        }

        // Core commands (via CoreCommandProvider, same pattern as plugins)
        var coreCommands = new CoreCommandProvider();
        foreach (var descriptor in coreCommands.GetCommands(services))
        {
            rootCommand.Subcommands.Add(descriptor.Command);
            OnCommandRegistered(descriptor.Command, descriptor);
        }

        // Plugin commands (same callback for unified handling)
        plugins.RegisterCommands(rootCommand, services, OnCommandRegistered);

        // Register subcommand orders for ALL commands (core + plugin-provided)
        // This must happen AFTER plugins have registered and merged their commands
        foreach (var cmd in rootCommand.Subcommands)
        {
            RegisterSubcommandOrders(cmd, orderRegistry);
        }

        // Pipeline step markers are now registered via IsSequentialStep on CommandDescriptor
        // (handled in OnCommandRegistered callback above — no DI lookup needed)

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
    /// Subcommand order definitions for host-internal parent commands.
    /// Maps parent command name → (subcommand name → order).
    /// Unknown subcommands (e.g., from plugins) get <see cref="CommandOrderRegistry.DefaultOrder"/>.
    /// </summary>
    /// <remarks>
    /// Only contains orders for commands added internally by a parent's Create() method.
    /// Plugin commands (generate/*, clean/*) set their own order via CommandDescriptor.
    /// </remarks>
    private static readonly FrozenDictionary<string, FrozenDictionary<string, int>> SubcommandOrders =
        new Dictionary<string, FrozenDictionary<string, int>>(StringComparer.Ordinal)
        {
            // Order within theme: list (10), files (20), extract (30)
            ["theme"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["list"] = 10,
                ["files"] = 20,
                ["extract"] = 30,
            }.ToFrozenDictionary(StringComparer.Ordinal),
            // Order within plugin: list (10), install (20), uninstall (30)
            ["plugin"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["list"] = 10,
                ["install"] = 20,
                ["uninstall"] = 30,
            }.ToFrozenDictionary(StringComparer.Ordinal),
            // Order within packages: refresh (10), search (20)
            ["packages"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["refresh"] = 10,
                ["search"] = 20,
            }.ToFrozenDictionary(StringComparer.Ordinal),
        }.ToFrozenDictionary(StringComparer.Ordinal);

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
    /// Config subcommand metadata for host-owned commands only.
    /// Plugin config commands set their own Group/Order/RequiresProject via <see cref="CommandDescriptor"/>.
    /// Commands not listed here and without plugin metadata get defaults (DefaultOrder, null group, requiresProject: true).
    /// </summary>
    private static readonly FrozenDictionary<string, (int Order, string? Group, bool RequiresProject)> ConfigSubcommandMeta =
        new Dictionary<string, (int Order, string? Group, bool RequiresProject)>(StringComparer.Ordinal)
        {
            // Project group: core project settings (host-owned, added in ConfigCommand.Create())
            // "project" doesn't require project - it creates project.json
            ["project"] = (10, CommandGroups.ConfigProject, false),
            ["site"] = (50, CommandGroups.ConfigProject, true),

            // Packages group: feed management (host-owned, global, no project needed)
            ["feed"] = (10, CommandGroups.ConfigPackages, false),

            // Ungrouped: locations (host-owned, global, no project needed)
            ["locations"] = (90, null, false),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Registers metadata for config subcommands.
    /// </summary>
    /// <remarks>
    /// Host-owned commands (project, site, feed, locations) get their metadata from <see cref="ConfigSubcommandMeta"/>.
    /// Plugin commands already have order/group/requiresProject set via <see cref="CommandDescriptor"/>
    /// and <c>OnCommandRegistered</c> — they are skipped here to avoid overriding plugin values.
    /// </remarks>
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
            // Skip commands that already have a group (set by plugins via CommandDescriptor)
            if (orderRegistry.GetGroup(sub) is not null)
            {
                continue;
            }

            // Only host-owned commands need metadata from the dict
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
