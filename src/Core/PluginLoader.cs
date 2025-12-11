using System.Runtime.Loader;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Core;

/// <summary>
/// Loads plugins from multiple directories based on configuration.
/// </summary>
/// <remarks>
/// Plugin search order:
/// 1. Application directory (for development/debugging via ProjectReference)
/// 2. Local plugin directory (next to exe in 'plugins' subfolder)
/// 3. User plugin directory (AppData/Revela/plugins - installed plugins)
/// 4. Additional search paths (custom locations)
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

    private static readonly string LocalPluginDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");

    private readonly ILogger<PluginLoader> logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginLoader>.Instance;
    private readonly HashSet<string> loadedAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IPlugin> loadedPlugins = [];
    private readonly List<AssemblyLoadContext> pluginContexts = []; // Keep contexts alive

    /// <summary>
    /// Gets the list of loaded plugins.
    /// </summary>
    public IReadOnlyList<IPlugin> GetLoadedPlugins() => loadedPlugins.AsReadOnly();

    /// <summary>
    /// Loads all plugins from configured directories.
    /// </summary>
    /// <remarks>
    /// Each plugin is loaded in its own AssemblyLoadContext for isolation.
    /// This prevents dependency version conflicts between plugins.
    /// </remarks>
    public void LoadPlugins()
    {
        // 1. Application directory (development - plugins built via ProjectReference)
        if (options.SearchApplicationDirectory)
        {
            LoadPluginsFromDirectory(ApplicationDirectory, "application");
        }

        // 2. Local plugin directory (next to exe in 'plugins' subfolder)
        LoadPluginsFromDirectory(LocalPluginDirectory, "local");

        // 3. User plugin directory (global installed plugins)
        if (options.SearchUserPluginDirectory)
        {
            LoadPluginsFromDirectory(UserPluginDirectory, "user");
        }

        // 4. Additional search paths
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

        // Load all DLLs from root directory (not subfolders)
        // Each DLL is checked if it implements IPlugin
        // Dependencies should be placed in a subfolder named after the plugin
        var pluginDlls = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);

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
        // Each plugin gets its own isolated AssemblyLoadContext
        // This allows different plugins to use different versions of the same dependency
        var loadContext = new PluginLoadContext(assemblyPath);
        pluginContexts.Add(loadContext); // Keep context alive

        var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        LogPluginContextCreated(Path.GetFileName(assemblyPath), loadContext.Name ?? "unnamed");

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
        // Using Span<T> to avoid substring allocation
        if (pattern.StartsWith('*'))
        {
            return fileName.AsSpan().EndsWith(pattern.AsSpan(1), StringComparison.OrdinalIgnoreCase);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created isolated load context '{ContextName}' for plugin {Assembly}")]
    private partial void LogPluginContextCreated(string assembly, string contextName);
}


