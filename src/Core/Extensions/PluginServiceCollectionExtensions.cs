using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering plugins with IServiceCollection.
/// </summary>
public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// Loads and registers plugins with the service collection.
    /// </summary>
    /// <remarks>
    /// This method:
    /// 1. Checks if command-line args indicate plugin management (skips loading if so)
    /// 2. Loads plugin assemblies from configured directories (app directory + user plugins + custom paths)
    /// 3. Calls ConfigureConfiguration() on each plugin (optional, framework auto-loads plugins/*.json)
    /// 4. Calls ConfigureServices() on each plugin (registers services with DI)
    /// 5. Returns IPluginContext for later Initialize() and RegisterCommands()
    ///
    /// Plugin search order:
    /// - Application directory (for development - plugins built via ProjectReference)
    /// - User plugin directory (~/.revela/plugins - installed plugins)
    /// - Additional search paths (custom locations)
    ///
    /// Plugin loading is skipped for 'plugin' commands (install, uninstall, list, etc.)
    /// because these commands manage plugin files and cannot work if DLLs are locked.
    ///
    /// Must be called BEFORE host.Build() so plugins can register services.
    ///
    /// Example:
    /// var builder = Host.CreateApplicationBuilder(args);
    /// var plugins = builder.Services.AddPlugins(builder.Configuration, args);
    /// var host = builder.Build();
    /// plugins.Initialize(host.Services);
    /// </remarks>
    /// <param name="services">Service collection to register plugin services with.</param>
    /// <param name="configuration">Configuration builder (from Host) to register plugin config sources.</param>
    /// <param name="args">Command-line arguments to detect plugin management commands.</param>
    /// <param name="configure">Optional configuration for plugin loading behavior.</param>
    /// <returns>Plugin context for initialization and command registration.</returns>
    public static IPluginContext AddPlugins(
        this IServiceCollection services,
        IConfigurationBuilder configuration,
        string[] args,
        Action<PluginOptions>? configure = null)
    {
        // Skip plugin loading for plugin management commands
        // These commands manage plugin files and can't work if DLLs are locked by AssemblyLoadContext
        if (IsPluginManagementCommand(args))
        {
            services.AddSingleton<IPluginContext>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<PluginContext>>();
                return new PluginContext([], logger);
            });
            return new EmptyPluginContext();
        }

        var options = new PluginOptions();
        configure?.Invoke(options);

        // Create null logger for plugin loading
        // Real logging will be available after host is built
        ILogger<PluginLoader> logger = NullLogger<PluginLoader>.Instance;

        // Load plugins from all configured directories
        var pluginLoader = new PluginLoader(options, logger);
        pluginLoader.LoadPlugins();
        var plugins = pluginLoader.GetLoadedPlugins().ToList();

        if (options.RequirePlugins && plugins.Count == 0)
        {
            throw new InvalidOperationException(
                "No plugins found and RequirePlugins=true. " +
                "Ensure plugins are either built with the application (ProjectReference) " +
                "or installed in the user plugin directory.");
        }

        // ============================================
        // CONFIGURATION PHASE
        // ============================================
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance (startup logging)

        // Auto-load all plugins/*.json files from working directory
        // JSON structure: { "Spectara.Revela.Plugin.X": { ... } } - Package-ID as root key
        var pluginsDir = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
        if (Directory.Exists(pluginsDir))
        {
            foreach (var jsonFile in Directory.GetFiles(pluginsDir, "*.json"))
            {
                configuration.AddJsonFile(jsonFile, optional: true, reloadOnChange: true);
                logger.LogDebug("Auto-loaded plugin config: {File}", Path.GetFileName(jsonFile));
            }
        }

        // Auto-load environment variables with global prefix
        // Allows: SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__SHAREURL=https://...
        configuration.AddEnvironmentVariables(prefix: "SPECTARA__REVELA__");
        logger.LogDebug("Loaded environment variables with prefix SPECTARA__REVELA__");

        // Phase 1: Plugins may register additional config sources (optional, usually empty)
        foreach (var plugin in plugins)
        {
            try
            {
                plugin.ConfigureConfiguration(configuration);
                logger.LogDebug("Plugin '{Name}' configured configuration sources", plugin.Metadata.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plugin '{Name}' failed to configure configuration", plugin.Metadata.Name);
            }
        }

        // Phase 2: Plugins register their services
        // (e.g., HttpClient, Commands, IOptions)
        foreach (var plugin in plugins)
        {
            try
            {
                plugin.ConfigureServices(services);
                logger.LogInformation("Plugin '{Name}' v{Version} registered services", plugin.Metadata.Name, plugin.Metadata.Version);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plugin '{Name}' failed to configure services", plugin.Metadata.Name);
            }
        }

        // Phase 3: Register all plugins for IEnumerable<IPlugin> injection
        foreach (var plugin in plugins)
        {
            services.AddSingleton(plugin);
            logger.LogDebug("Registered plugin: {Name}", plugin.Metadata.Name);
        }

        // Phase 4: Register theme plugins for IThemeResolver (as IThemePlugin)
        foreach (var plugin in plugins.OfType<IThemePlugin>())
        {
            services.AddSingleton(plugin);
            logger.LogDebug("Registered theme plugin: {Name}", plugin.Metadata.Name);
        }

        // Phase 4b: Register theme extensions for IThemeResolver (as IThemeExtension)
        foreach (var extension in plugins.OfType<IThemeExtension>())
        {
            services.AddSingleton(extension);
            logger.LogDebug("Registered theme extension: {Name} for theme {TargetTheme}",
                extension.Metadata.Name, extension.TargetTheme);
        }
#pragma warning restore CA1848

        // Phase 5: Register PluginContext as singleton for UseRevelaCommands()
        // Uses factory to resolve ILogger<PluginContext> from built ServiceProvider
        services.AddSingleton<IPluginContext>(sp =>
        {
            var contextLogger = sp.GetRequiredService<ILogger<PluginContext>>();
            return new PluginContext(plugins, contextLogger);
        });

        // Return placeholder context for fluent API compatibility
        // The actual context will be resolved from DI with proper logger
        return new PluginContextPlaceholder(plugins);
    }

    /// <summary>
    /// Checks if the command-line arguments indicate a plugin management command.
    /// </summary>
    /// <remarks>
    /// Plugin management commands (install, uninstall, list, source) don't need loaded plugins
    /// and must be able to modify plugin files without them being locked.
    /// </remarks>
    private static bool IsPluginManagementCommand(string[] args)
        => args.Length > 0 && args[0].Equals("plugin", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Placeholder context returned by AddPlugins() before ServiceProvider is built.
/// The real PluginContext is resolved from DI in UseRevelaCommands().
/// </summary>
sealed file class PluginContextPlaceholder(IReadOnlyList<IPlugin> plugins) : IPluginContext
{
    public IReadOnlyList<IPlugin> Plugins => plugins;

    public void Initialize(IServiceProvider serviceProvider)
    {
        // Resolve real context from DI and delegate
        var realContext = serviceProvider.GetRequiredService<IPluginContext>();
        realContext.Initialize(serviceProvider);
    }

    public void RegisterCommands(System.CommandLine.RootCommand rootCommand)
    {
        throw new InvalidOperationException(
            "RegisterCommands should be called on the real PluginContext resolved from DI. " +
            "Use host.Services.GetRequiredService<IPluginContext>().RegisterCommands().");
    }
}

/// <summary>
/// Empty plugin context for plugin management commands.
/// </summary>
/// <remarks>
/// Used when plugin loading is skipped to allow file operations on plugin DLLs.
/// </remarks>
sealed file class EmptyPluginContext : IPluginContext
{
    public IReadOnlyList<IPlugin> Plugins => [];

    public void Initialize(IServiceProvider serviceProvider)
    {
        // No plugins to initialize
    }

    public void RegisterCommands(System.CommandLine.RootCommand rootCommand)
    {
        // No plugin commands to register
    }
}
