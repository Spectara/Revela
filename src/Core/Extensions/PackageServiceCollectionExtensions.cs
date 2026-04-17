using Microsoft.Extensions.Configuration;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering packages (plugins and themes) with IServiceCollection.
/// </summary>
public static class PackageServiceCollectionExtensions
{
    /// <summary>
    /// Loads and registers plugins and themes with the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="packageSource">Source that provides plugins and themes.</param>
    /// <param name="configuration">The configuration builder for plugin configuration.</param>
    /// <param name="args">CLI arguments (used to detect package management commands).</param>
    public static void AddPackages(
        this IServiceCollection services,
        IPackageSource packageSource,
        IConfigurationBuilder configuration,
        string[] args)
    {
        if (IsPackageManagementCommand(args))
        {
            services.AddSingleton<IPackageContext>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<PackageContext>>();
                return new PackageContext([], [], logger);
            });
            return;
        }

        var plugins = packageSource.LoadPlugins().ToList();
        var themes = packageSource.LoadThemes().ToList();

        using var loggerFactory = CreateBootstrapLoggerFactory();
        ValidatePluginDependencies(plugins, loggerFactory);
        ConfigurePlugins(services, configuration, plugins, loggerFactory);
        RegisterServices(services, plugins, themes);
    }

    private static ILoggerFactory CreateBootstrapLoggerFactory() =>
        LoggerFactory.Create(_ => { });

    private static void ValidatePluginDependencies(
        List<LoadedPluginInfo> plugins,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Spectara.Revela.Core.PluginBootstrap");

        var loadedIds = new HashSet<string>(
            plugins.Select(p => p.Plugin.Metadata.Id),
            StringComparer.OrdinalIgnoreCase);

        bool removedAny;
        do
        {
            removedAny = false;
            for (var i = plugins.Count - 1; i >= 0; i--)
            {
                var plugin = plugins[i].Plugin;
                var missing = plugin.Metadata.RequiredPackages
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

        foreach (var pluginInfo in plugins)
        {
            var plugin = pluginInfo.Plugin;
            var missingExtensions = plugin.Metadata.ExtendsPackages
                .Where(ext => !loadedIds.Contains(ext))
                .ToList();

            if (missingExtensions.Count > 0)
            {
                var extList = string.Join(", ", missingExtensions);
                logger.PluginExtensionTargetMissing(plugin.Metadata.Name, extList);
            }
        }
    }

    private static void ConfigurePlugins(
        IServiceCollection services,
        IConfigurationBuilder configuration,
        List<LoadedPluginInfo> plugins,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Spectara.Revela.Core.PluginBootstrap");

        configuration.AddEnvironmentVariables(prefix: "SPECTARA__REVELA__");

        foreach (var pluginInfo in plugins)
        {
            try
            {
                pluginInfo.Plugin.ConfigureConfiguration(configuration);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Error: Plugin '{pluginInfo.Plugin.Metadata.Name}' failed to configure configuration: {ex.Message}");
                logger.ConfigureConfigurationFailed(ex, pluginInfo.Plugin.Metadata.Name);
            }
        }

        foreach (var pluginInfo in plugins)
        {
            try
            {
                pluginInfo.Plugin.ConfigureServices(services);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Error: Plugin '{pluginInfo.Plugin.Metadata.Name}' failed to configure services: {ex.Message}");
                logger.ConfigureServicesFailed(ex, pluginInfo.Plugin.Metadata.Name);
            }
        }
    }

    /// <summary>
    /// Registers loaded plugins, themes, and IPluginContext in the DI container.
    /// </summary>
    private static void RegisterServices(
        IServiceCollection services,
        List<LoadedPluginInfo> plugins,
        List<LoadedThemeInfo> themes)
    {
        // Register all plugins for IEnumerable<IPlugin> injection
        foreach (var pluginInfo in plugins)
        {
            services.AddSingleton(pluginInfo.Plugin);
        }

        // Register all theme providers for IEnumerable<ITheme> injection
        foreach (var themeInfo in themes)
        {
            services.AddSingleton(themeInfo.Theme);
        }

        // Register PackageContext with both plugins and themes
        services.AddSingleton<IPackageContext>(sp =>
        {
            var contextLogger = sp.GetRequiredService<ILogger<PackageContext>>();
            return new PackageContext(plugins, themes, contextLogger);
        });
    }

    /// <summary>
    /// Checks if the command modifies plugin/theme files on disk.
    /// When true, package loading is skipped to avoid locking DLLs.
    /// </summary>
    private static bool IsPackageManagementCommand(string[] args)
    {
        if (args.Length < 2)
        {
            return false;
        }

        var command = args[0];
        var subcommand = args[1];

        // "plugin list" and "theme list" are safe (read-only)
        if (subcommand.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // "plugin install/uninstall" or "theme install/uninstall" modify files
        return command.Equals("plugin", StringComparison.OrdinalIgnoreCase)
            || command.Equals("theme", StringComparison.OrdinalIgnoreCase);
    }
}


