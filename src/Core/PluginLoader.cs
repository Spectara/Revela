using System.Reflection;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Core;

/// <summary>
/// Loads plugins from multiple directories based on configuration.
/// </summary>
/// <remarks>
/// Plugin search order:
/// 1. Application directory (for development/debugging via ProjectReference)
/// 2. User plugin directory (AppData/Revela/plugins - installed plugins)
/// 3. Additional search paths (custom locations)
///
/// This unified approach ensures Debug and Release builds use identical code paths.
/// </remarks>
public sealed partial class PluginLoader(
    PluginOptions options,
    ILogger<PluginLoader>? logger = null)
{
    private static readonly string UserPluginDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Revela",
        "plugins");

    private static readonly string ApplicationDirectory = AppContext.BaseDirectory;

    private readonly ILogger<PluginLoader> logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginLoader>.Instance;
    private readonly HashSet<string> loadedAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IPlugin> loadedPlugins = [];

    /// <summary>
    /// Gets the list of loaded plugins.
    /// </summary>
    public IReadOnlyList<IPlugin> GetLoadedPlugins() => loadedPlugins.AsReadOnly();

    /// <summary>
    /// Loads all plugins from configured directories.
    /// </summary>
    public void LoadPlugins()
    {
        // 1. Application directory (development - plugins built via ProjectReference)
        if (options.SearchApplicationDirectory)
        {
            LoadPluginsFromDirectory(ApplicationDirectory, "application");
        }

        // 2. User plugin directory (installed plugins)
        if (options.SearchUserPluginDirectory)
        {
            LoadPluginsFromDirectory(UserPluginDirectory, "user");
        }

        // 3. Additional search paths
        foreach (var path in options.AdditionalSearchPaths)
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            LoadPluginsFromDirectory(fullPath, "additional");
        }

        LogPluginsLoaded(loadedPlugins.Count);
    }

    private void LoadPluginsFromDirectory(string directory, string source)
    {
        if (!Directory.Exists(directory))
        {
            LogPluginDirectoryNotFound(directory, source);
            return;
        }

        // Search pattern: Spectara.Revela.Plugin.*.dll (official naming convention)
        var pluginPattern = "Spectara.Revela.Plugin.*.dll";
        var pluginDlls = Directory.GetFiles(directory, pluginPattern, SearchOption.TopDirectoryOnly);

        if (options.EnableVerboseLogging)
        {
            LogSearchingDirectory(directory, source, pluginDlls.Length);
        }

        foreach (var dll in pluginDlls)
        {
            // Skip if already loaded (prevents duplicates from multiple directories)
            if (loadedAssemblyPaths.Contains(dll))
            {
                continue;
            }

            // Skip excluded patterns
            var fileName = Path.GetFileName(dll);
            if (options.ExcludePatterns.Any(pattern => MatchesPattern(fileName, pattern)))
            {
                LogPluginExcluded(fileName);
                continue;
            }

            try
            {
                LoadPluginFromAssembly(dll);
                loadedAssemblyPaths.Add(dll);
            }
            catch (Exception ex)
            {
                LogPluginLoadFailed(ex, dll);
            }
        }
    }

    private void LoadPluginFromAssembly(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);

        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in pluginTypes)
        {
            // Check if plugin with same name already loaded (prevents duplicates)
            var plugin = (IPlugin?)Activator.CreateInstance(type);
            if (plugin is null)
            {
                continue;
            }

            if (loadedPlugins.Any(p => p.Metadata.Name == plugin.Metadata.Name))
            {
                LogPluginDuplicate(plugin.Metadata.Name, assemblyPath);
                continue;
            }

            loadedPlugins.Add(plugin);
            LogPluginDiscovered(plugin.Metadata.Name, plugin.Metadata.Version, Path.GetFileName(assemblyPath));
        }
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        // Simple wildcard matching: *.Tests.dll
        if (pattern.StartsWith('*'))
        {
            return fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    // High-performance logging using source generator
    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin directory does not exist: {Directory} ({Source})")]
    private partial void LogPluginDirectoryNotFound(string directory, string source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Searching {Directory} ({Source}): found {Count} plugin candidate(s)")]
    private partial void LogSearchingDirectory(string directory, string source, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin excluded by pattern: {FileName}")]
    private partial void LogPluginExcluded(string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{Name}' already loaded, skipping duplicate from {Assembly}")]
    private partial void LogPluginDuplicate(string name, string assembly);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load plugin from {Assembly}")]
    private partial void LogPluginLoadFailed(Exception exception, string assembly);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} plugin(s)")]
    private partial void LogPluginsLoaded(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered plugin: {Name} v{Version} ({Assembly})")]
    private partial void LogPluginDiscovered(string name, string version, string assembly);
}


