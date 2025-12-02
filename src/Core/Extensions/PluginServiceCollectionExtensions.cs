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
    /// 1. Loads plugin assemblies from configured directories (app directory + user plugins + custom paths)
    /// 2. Calls ConfigureConfiguration() on each plugin (registers config sources like onedrive.json)
    /// 3. Calls ConfigureServices() on each plugin (registers services with DI)
    /// 4. Returns IPluginContext for later Initialize() and RegisterCommands()
    ///
    /// Plugin search order:
    /// - Application directory (for development - plugins built via ProjectReference)
    /// - User plugin directory (~/.revela/plugins - installed plugins)
    /// - Additional search paths (custom locations)
    ///
    /// Must be called BEFORE host.Build() so plugins can register services.
    ///
    /// Example:
    /// var builder = Host.CreateApplicationBuilder(args);
    /// var plugins = builder.Services.AddPlugins(builder.Configuration);
    /// var host = builder.Build();
    /// plugins.Initialize(host.Services);
    /// </remarks>
    /// <param name="services">Service collection to register plugin services with.</param>
    /// <param name="configuration">Configuration builder (from Host) to register plugin config sources.</param>
    /// <param name="configure">Optional configuration for plugin loading behavior.</param>
    /// <returns>Plugin context for initialization and command registration.</returns>
    public static IPluginContext AddPlugins(
        this IServiceCollection services,
        IConfigurationBuilder configuration,
        Action<PluginOptions>? configure = null)
    {
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

        // Phase 1: Plugins register their configuration sources
        // (e.g., onedrive.json, environment variables)
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance (startup logging)
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
#pragma warning restore CA1848

        // Return context for later Initialize() and RegisterCommands()
        return new PluginContext(plugins);
    }
}
