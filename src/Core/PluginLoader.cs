using System.Reflection;
using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Core;

/// <summary>
/// Loads plugins from the user's plugin directory
/// </summary>
public sealed partial class PluginLoader
{
    private readonly string pluginDirectory;
    private readonly ILogger<PluginLoader> logger;
    private readonly List<IPlugin> loadedPlugins = [];

    public PluginLoader(ILogger<PluginLoader>? logger = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        this.pluginDirectory = Path.Combine(appData, "Revela", "plugins");
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginLoader>.Instance;
    }

    /// <summary>
    /// Gets the list of loaded plugins
    /// </summary>
    public IReadOnlyList<IPlugin> GetLoadedPlugins() => loadedPlugins.AsReadOnly();

    /// <summary>
    /// Loads all plugins from the plugin directory
    /// </summary>
    public void LoadPlugins()
    {
        if (!Directory.Exists(pluginDirectory))
        {
            LogPluginDirectoryNotFound(logger, pluginDirectory);
            return;
        }

        var pluginDlls = Directory.GetFiles(pluginDirectory, "Revela.Plugin.*.dll", SearchOption.AllDirectories);

        foreach (var dll in pluginDlls)
        {
            try
            {
                LoadPluginFromAssembly(dll);
            }
            catch (Exception ex)
            {
                LogPluginLoadFailed(logger, ex, dll);
            }
        }

        LogPluginsLoaded(logger, loadedPlugins.Count);
    }

    private void LoadPluginFromAssembly(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);

        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in pluginTypes)
        {
            var plugin = (IPlugin?)Activator.CreateInstance(type);
            if (plugin is not null)
            {
                loadedPlugins.Add(plugin);
                LogPluginDiscovered(logger, plugin.Metadata.Name, plugin.Metadata.Version);
            }
        }
    }

    // High-performance logging using source generator
    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin directory does not exist: {Directory}")]
    private static partial void LogPluginDirectoryNotFound(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load plugin from {Assembly}")]
    private static partial void LogPluginLoadFailed(ILogger logger, Exception exception, string assembly);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} plugin(s)")]
    private static partial void LogPluginsLoaded(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded plugin: {Name} v{Version}")]
    private static partial void LogPluginDiscovered(ILogger logger, string name, string version);
}


