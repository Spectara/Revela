using Microsoft.Extensions.Configuration;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

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
    /// <para>
    /// Orchestrates plugin discovery and registration in 3 phases:
    /// </para>
    /// <list type="number">
    /// <item>Load plugin assemblies from configured directories</item>
    /// <item>Call ConfigureConfiguration() + ConfigureServices() on each plugin</item>
    /// <item>Register plugins, themes, and extensions in DI</item>
    /// </list>
    /// <para>
    /// Plugin loading is skipped for management commands (install, uninstall)
    /// because these commands modify plugin files and cannot work if DLLs are locked.
    /// </para>
    /// <para>
    /// Must be called BEFORE host.Build(). After build, resolve <see cref="IPluginContext"/>
    /// from DI to register commands.
    /// </para>
    /// </remarks>
    /// <param name="services">Service collection to register plugin services with.</param>
    /// <param name="configuration">Configuration builder to register plugin config sources.</param>
    /// <param name="args">Command-line arguments to detect plugin management commands.</param>
    /// <param name="configure">Optional configuration for plugin loading behavior.</param>
    public static void AddPlugins(
        this IServiceCollection services,
        IConfigurationBuilder configuration,
        string[] args,
        Action<PluginOptions>? configure = null)
    {
        // Skip plugin loading for plugin management commands (except 'plugin list')
        if (IsPluginManagementCommand(args))
        {
            services.AddSingleton<IPluginContext>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<PluginContext>>();
                return new PluginContext([], logger);
            });
            return;
        }

        var options = new PluginOptions();
        configure?.Invoke(options);

        using var loggerFactory = CreateBootstrapLoggerFactory();
        var plugins = LoadPlugins(options, loggerFactory);
        ValidatePluginDependencies(plugins, loggerFactory);
        ConfigurePlugins(services, configuration, plugins, loggerFactory);
        RegisterPluginServices(services, plugins);
    }

    /// <summary>
    /// Creates a minimal logger for the bootstrap phase (before DI is available).
    /// </summary>
    /// <remarks>
    /// Uses a simple LoggerFactory without providers. The LoggerMessage source generator
    /// still works (methods are no-ops when no provider is attached), but errors in
    /// the <c>ConfigurePlugins</c> catch blocks are written to stderr to ensure
    /// plugin failures are always visible.
    /// </remarks>
    private static ILoggerFactory CreateBootstrapLoggerFactory() =>
        LoggerFactory.Create(_ => { });

    /// <summary>
    /// Discovers and loads plugin assemblies from configured directories.
    /// </summary>
    private static List<LoadedPluginInfo> LoadPlugins(
        PluginOptions options,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<PluginLoader>();
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

        return plugins;
    }

    /// <summary>
    /// Validates plugin dependencies and removes plugins with unmet requirements.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Checks <see cref="PluginMetadata.RequiredPlugins"/> for each plugin.
    /// If any required plugin is not loaded, the dependent plugin is removed with an error.
    /// </para>
    /// <para>
    /// <see cref="PluginMetadata.ExtendsPlugins"/> are informational only —
    /// extension commands are skipped at registration time if the parent plugin is absent.
    /// </para>
    /// </remarks>
    private static void ValidatePluginDependencies(
        List<LoadedPluginInfo> plugins,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Spectara.Revela.Core.PluginBootstrap");

        // Use fully qualified Id for dependency matching (not display Name)
        var loadedIds = new HashSet<string>(
            plugins.Select(p => p.Plugin.Metadata.Id),
            StringComparer.OrdinalIgnoreCase);

        // Multiple passes to handle transitive dependencies
        bool removedAny;
        do
        {
            removedAny = false;
            for (var i = plugins.Count - 1; i >= 0; i--)
            {
                var plugin = plugins[i].Plugin;
                var missing = plugin.Metadata.RequiredPlugins
                    .Where(req => !loadedIds.Contains(req))
                    .ToList();

                if (missing.Count > 0)
                {
                    var missingList = string.Join(", ", missing);
                    Console.Error.WriteLine(
                        $"Error: Plugin '{plugin.Metadata.Name}' requires [{missingList}] but they are not installed. Plugin will not be loaded.");
                    logger.PluginDependencyMissing(plugin.Metadata.Name, missingList);

                    loadedIds.Remove(plugin.Metadata.Id);
                    plugins.RemoveAt(i);
                    removedAny = true;
                }
            }
        }
        while (removedAny);

        // Log info for optional extensions with missing targets
        foreach (var pluginInfo in plugins)
        {
            var plugin = pluginInfo.Plugin;
            var missingExtensions = plugin.Metadata.ExtendsPlugins
                .Where(ext => !loadedIds.Contains(ext))
                .ToList();

            if (missingExtensions.Count > 0)
            {
                var extList = string.Join(", ", missingExtensions);
                logger.PluginExtensionTargetMissing(plugin.Metadata.Name, extList);
            }
        }
    }

    /// <summary>
    /// Calls ConfigureConfiguration() and ConfigureServices() on each loaded plugin.
    /// </summary>
    private static void ConfigurePlugins(
        IServiceCollection services,
        IConfigurationBuilder configuration,
        List<LoadedPluginInfo> plugins,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Spectara.Revela.Core.PluginBootstrap");

        // Auto-load environment variables with global prefix
        configuration.AddEnvironmentVariables(prefix: "SPECTARA__REVELA__");

        // Phase 1: Plugins may register additional config sources (optional)
        foreach (var pluginInfo in plugins)
        {
            try
            {
                pluginInfo.Plugin.ConfigureConfiguration(configuration);
            }
            catch (Exception ex)
            {
                // Write to stderr directly — bootstrap phase has no DI logger yet
                Console.Error.WriteLine(
                    $"Error: Plugin '{pluginInfo.Plugin.Metadata.Name}' failed to configure configuration: {ex.Message}");
                logger.ConfigureConfigurationFailed(ex, pluginInfo.Plugin.Metadata.Name);
            }
        }

        // Phase 2: Plugins register their services (HttpClient, Commands, IOptions)
        foreach (var pluginInfo in plugins)
        {
            try
            {
                pluginInfo.Plugin.ConfigureServices(services);
            }
            catch (Exception ex)
            {
                // Write to stderr directly — bootstrap phase has no DI logger yet
                Console.Error.WriteLine(
                    $"Error: Plugin '{pluginInfo.Plugin.Metadata.Name}' failed to configure services: {ex.Message}");
                logger.ConfigureServicesFailed(ex, pluginInfo.Plugin.Metadata.Name);
            }
        }
    }

    /// <summary>
    /// Registers loaded plugins, themes, and extensions in the DI container.
    /// </summary>
    private static void RegisterPluginServices(
        IServiceCollection services,
        List<LoadedPluginInfo> plugins)
    {
        // Register all plugins for IEnumerable<IPlugin> injection
        foreach (var pluginInfo in plugins)
        {
            services.AddSingleton(pluginInfo.Plugin);
        }

        // Register theme plugins for IThemeResolver
        foreach (var plugin in plugins.Select(p => p.Plugin).OfType<ITheme>())
        {
            services.AddSingleton(plugin);
        }

        // Register theme extensions for IThemeResolver
        foreach (var extension in plugins.Select(p => p.Plugin).OfType<IThemeExtension>())
        {
            services.AddSingleton(extension);
        }

        // Register PluginContext as singleton — resolved in UseRevelaCommands()
        services.AddSingleton<IPluginContext>(sp =>
        {
            var contextLogger = sp.GetRequiredService<ILogger<PluginContext>>();
            return new PluginContext(plugins, contextLogger);
        });
    }

    /// <summary>
    /// Checks if the command-line arguments indicate a plugin management command.
    /// </summary>
    private static bool IsPluginManagementCommand(string[] args)
    {
        if (args.Length == 0 || !args[0].Equals("plugin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // "plugin list" can safely load plugins (read-only operation)
        if (args.Length >= 2 && args[1].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
