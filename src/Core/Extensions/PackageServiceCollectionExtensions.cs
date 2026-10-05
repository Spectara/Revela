using Spectara.Revela.Core;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering packages (plugins and themes) with IServiceCollection.
/// </summary>
public static class PackageServiceCollectionExtensions
{
    private const string BootstrapLoggerCategory = "Spectara.Revela.Core.PluginBootstrap";

    /// <summary>
    /// Loads and registers plugins and themes with the service collection.
    /// </summary>
    /// <remarks>
    /// A plugin whose <see cref="IPlugin.ConfigureServices"/> throws is skipped completely: its
    /// registrations are discarded, it is not registered and contributes no commands, and one
    /// warning is logged. Revela continues with the remaining plugins.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="packageSource">Source that provides plugins and themes.</param>
    /// <param name="args">CLI arguments (used to detect package management commands).</param>
    /// <param name="loggerFactory">
    /// Logger factory for problems found while packages are loaded, before the host's loggers exist.
    /// </param>
    /// <exception cref="PluginConfigConflictException">Two loaded packages claim the same <c>plugins:&lt;key&gt;</c>.</exception>
    public static void AddPackages(
        this IServiceCollection services,
        IPackageSource packageSource,
        string[] args,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        // plugin|theme install/uninstall replace or delete package files: don't load (and lock) them
        if (PackageManagementCommands.ModifiesPackageFiles(args))
        {
            // No package is loaded, so nothing claims a plugins:<key> node.
            services.AddSingleton(PluginConfigOwnership.FromClaims([]));
            services.AddSingleton<IPackageContext>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<PackageContext>>();
                return new PackageContext([], [], logger);
            });
            return;
        }

        var plugins = packageSource.LoadPlugins().ToList();
        var themes = packageSource.LoadThemes().ToList();

        var logger = loggerFactory.CreateLogger(BootstrapLoggerCategory);
        ValidatePluginDependencies(plugins, logger);

        // Resolve plugins:<key> ownership before any plugin configures services, so a
        // duplicate claim fails loading instead of two packages binding the same node.
        var ownership = PluginConfigOwnership.FromPackages(
            plugins.Select(p => (IPackage)p.Plugin).Concat(themes.Select(t => t.Theme)));
        services.AddSingleton(ownership);
        services.AddSingleton<UnclaimedPluginConfigReporter>();

        var configured = ConfigurePlugins(services, plugins, logger);
        RegisterServices(services, configured, themes);
    }

    private static void ValidatePluginDependencies(
        List<LoadedPluginInfo> plugins,
        ILogger logger)
    {
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
                    logger.PluginDependencyMissing(plugin.Metadata.Name, string.Join(", ", missing));

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

    /// <summary>
    /// Lets each plugin configure services, all or nothing per plugin.
    /// </summary>
    /// <remarks>
    /// Each plugin works on a scratch copy of the collection, so <c>TryAdd*</c> sees the host's
    /// registrations and <c>Replace</c>/<c>RemoveAll</c> work as usual. Only when
    /// <see cref="IPlugin.ConfigureServices"/> returns is the copy taken over; when it throws,
    /// the half-applied changes are dropped with it.
    /// </remarks>
    /// <returns>The plugins that configured their services successfully.</returns>
    private static List<LoadedPluginInfo> ConfigurePlugins(
        IServiceCollection services,
        List<LoadedPluginInfo> plugins,
        ILogger logger)
    {
        var configured = new List<LoadedPluginInfo>(plugins.Count);

        foreach (var pluginInfo in plugins)
        {
            IServiceCollection scratch = new ServiceCollection();
            foreach (var descriptor in services)
            {
                scratch.Add(descriptor);
            }

            try
            {
                pluginInfo.Plugin.ConfigureServices(scratch);
            }
            catch (Exception ex)
            {
                logger.ConfigureServicesFailed(pluginInfo.Plugin.Metadata.Name, ex.Message);
                logger.ConfigureServicesFailedDetails(ex, pluginInfo.Plugin.Metadata.Name);
                continue;
            }

            services.Clear();
            foreach (var descriptor in scratch)
            {
                services.Add(descriptor);
            }

            configured.Add(pluginInfo);
        }

        return configured;
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
}


