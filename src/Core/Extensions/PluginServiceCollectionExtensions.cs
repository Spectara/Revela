using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk.Abstractions;

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

        var plugins = LoadPlugins(options);
        ConfigurePlugins(services, configuration, plugins);
        RegisterPluginServices(services, plugins);
    }

    /// <summary>
    /// Discovers and loads plugin assemblies from configured directories.
    /// </summary>
    private static List<ILoadedPluginInfo> LoadPlugins(PluginOptions options)
    {
        var logger = NullLogger<PluginLoader>.Instance;
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
    /// Calls ConfigureConfiguration() and ConfigureServices() on each loaded plugin.
    /// </summary>
    private static void ConfigurePlugins(
        IServiceCollection services,
        IConfigurationBuilder configuration,
        List<ILoadedPluginInfo> plugins)
    {
        var logger = NullLogger<PluginLoader>.Instance;

#pragma warning disable CA1848 // Use LoggerMessage delegates for performance (startup logging)

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
                logger.LogError(ex, "Plugin '{Name}' failed to configure configuration", pluginInfo.Plugin.Metadata.Name);
            }
        }

        // Phase 2: Plugins register their services (HttpClient, Commands, IOptions)
        foreach (var pluginInfo in plugins)
        {
            try
            {
                pluginInfo.Plugin.ConfigureServices(services);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Plugin '{Name}' v{Version} registered services",
                        pluginInfo.Plugin.Metadata.Name, pluginInfo.Plugin.Metadata.Version);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plugin '{Name}' failed to configure services", pluginInfo.Plugin.Metadata.Name);
            }
        }

#pragma warning restore CA1848
    }

    /// <summary>
    /// Registers loaded plugins, themes, and extensions in the DI container.
    /// </summary>
    private static void RegisterPluginServices(
        IServiceCollection services,
        List<ILoadedPluginInfo> plugins)
    {
        // Register all plugins for IEnumerable<IPlugin> injection
        foreach (var pluginInfo in plugins)
        {
            services.AddSingleton(pluginInfo.Plugin);
        }

        // Register theme plugins for IThemeResolver
        foreach (var plugin in plugins.Select(p => p.Plugin).OfType<IThemePlugin>())
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
