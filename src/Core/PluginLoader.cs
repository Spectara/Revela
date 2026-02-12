using System.Reflection;
using System.Runtime.Loader;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core;

/// <summary>
/// Loads plugins from configured directories.
/// </summary>
/// <remarks>
/// Plugin search order:
/// 1. Application directory (for development/debugging via ProjectReference)
/// 2. Plugin directory (context-aware: exe-dir/plugins for standalone, AppData/plugins for dotnet tool)
///
/// This unified approach ensures Debug and Release builds use identical code paths.
/// The plugin directory is determined by ConfigPathResolver based on installation type.
/// </remarks>
public sealed partial class PluginLoader(
    PluginOptions options,
    ILogger<PluginLoader>? logger = null)
{
    /// <summary>
    /// Gets the plugin directory based on installation type.
    /// Standalone: {exe-dir}/plugins
    /// dotnet tool: %APPDATA%/Revela/plugins
    /// </summary>
    private static readonly string PluginDirectory = ConfigPathResolver.LocalPluginDirectory;

    private static readonly string ApplicationDirectory = AppContext.BaseDirectory;

    private readonly ILogger<PluginLoader> logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginLoader>.Instance;
    private readonly HashSet<string> loadedAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ILoadedPluginInfo> loadedPlugins = [];
    private readonly List<AssemblyLoadContext> pluginContexts = []; // Keep contexts alive

    /// <summary>
    /// Gets the list of loaded plugins with their source information.
    /// </summary>
    public IReadOnlyList<ILoadedPluginInfo> GetLoadedPlugins() => loadedPlugins.AsReadOnly();

    /// <summary>
    /// Gets all loaded theme extensions.
    /// </summary>
    public IReadOnlyList<IThemeExtension> GetThemeExtensions() =>
        loadedPlugins
            .Select(p => p.Plugin)
            .OfType<IThemeExtension>()
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Gets theme extensions for a specific theme.
    /// </summary>
    /// <param name="themeName">Theme name to match (case-insensitive)</param>
    /// <returns>List of extensions targeting the specified theme</returns>
    public IReadOnlyList<IThemeExtension> GetThemeExtensions(string themeName) =>
        loadedPlugins
            .Select(p => p.Plugin)
            .OfType<IThemeExtension>()
            .Where(e => e.TargetTheme.Equals(themeName, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();

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
            LoadPluginsFromDirectory(ApplicationDirectory, "application", PluginSource.Bundled);
        }

        // 2. Plugin directory (context-aware: exe-dir/plugins or AppData/plugins)
        LoadPluginsFromDirectory(PluginDirectory, "installed", PluginSource.Local);

        LogPluginsLoaded(loadedPlugins.Count);
    }

    private void LoadPluginsFromDirectory(string directory, string sourceLabel, PluginSource source)
    {
        if (!Directory.Exists(directory))
        {
            LogPluginDirectoryNotFound(directory, sourceLabel);
            return;
        }

        // Load plugin DLLs from:
        // 1. Root directory (development - plugins built via ProjectReference)
        // 2. Subdirectories (installed plugins - each in own folder with dependencies)
        var rootDlls = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
        var subDirDlls = Directory.GetDirectories(directory)
            .Select(subDir =>
            {
                // Look for main plugin DLL matching folder name
                var folderName = Path.GetFileName(subDir);
                var mainDll = Path.Combine(subDir, $"{folderName}.dll");
                return File.Exists(mainDll) ? mainDll : null;
            })
            .Where(dll => dll is not null)
            .Cast<string>()
            .ToArray();

        var pluginDlls = rootDlls.Concat(subDirDlls).ToArray();

        if (options.EnableVerboseLogging)
        {
            LogSearchingDirectory(directory, sourceLabel, pluginDlls.Length);
        }

        // Determine if we should use default context (for app directory = development mode)
        var useDefaultContext = sourceLabel == "application";

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
                LoadPluginFromAssembly(dll, useDefaultContext, source);
                loadedAssemblyPaths.Add(dll);
            }
            catch (ReflectionTypeLoadException rtle)
            {
                // Log detailed info for debugging
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    LogPluginReflectionLoadFailed(fileName, rtle.Message);
                }
                foreach (var ex in rtle.LoaderExceptions.Where(e => e != null).Take(3))
                {
                    if (ex is FileNotFoundException fnf && !string.IsNullOrEmpty(fnf.FileName))
                    {
                        LogMissingDependency(fnf.FileName);
                    }
                }

                LogPluginLoadFailed(rtle, dll);
            }
            catch (Exception ex)
            {
                LogPluginLoadFailed(ex, dll);
            }
        }
    }

    private void LoadPluginFromAssembly(string assemblyPath, bool useDefaultContext, PluginSource source)
    {
        var assemblyFileName = Path.GetFileName(assemblyPath);
        Assembly assembly;

        if (useDefaultContext)
        {
            // Development mode: Load from default context (same as host)
            // This ensures all types are compatible (e.g., PanelStyles extension methods)
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                LogPluginContextCreated(assemblyFileName, "Default (development)");
            }
        }
        else
        {
            // Production mode: Each plugin gets its own isolated AssemblyLoadContext
            // This allows different plugins to use different versions of the same dependency
            var loadContext = new PluginLoadContext(assemblyPath);
            pluginContexts.Add(loadContext); // Keep context alive

            assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                LogPluginContextCreated(assemblyFileName, loadContext.Name ?? "unnamed");
            }
        }

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

            if (loadedPlugins.Any(p => p.Plugin.Metadata.Name == plugin.Metadata.Name))
            {
                LogPluginDuplicate(plugin.Metadata.Name, assemblyPath);
                continue;
            }

            // Check SDK version compatibility
            CheckSdkVersionCompatibility(assembly, plugin.Metadata.Name);

            loadedPlugins.Add(new LoadedPluginInfo(plugin, source));
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogPluginDiscovered(plugin.Metadata.Name, plugin.Metadata.Version, assemblyFileName);
            }
        }
    }

    /// <summary>
    /// Checks if the plugin was compiled against a compatible SDK version.
    /// </summary>
    /// <remarks>
    /// Plugins should be compiled against the same or older SDK version as the host.
    /// If a plugin uses a newer SDK, it may call methods that don't exist in the host's SDK,
    /// causing MissingMethodException at runtime.
    /// </remarks>
    private void CheckSdkVersionCompatibility(Assembly pluginAssembly, string pluginName)
    {
        var pluginSdkRef = pluginAssembly.GetReferencedAssemblies()
            .FirstOrDefault(a => a.Name == "Spectara.Revela.Sdk");

        if (pluginSdkRef?.Version is null)
        {
            return; // Plugin doesn't reference SDK (unusual but possible)
        }

        var hostSdkAssembly = typeof(IPlugin).Assembly;
        var hostSdkVersion = hostSdkAssembly.GetName().Version;

        if (hostSdkVersion is null)
        {
            return; // Host SDK version unknown
        }

        if (pluginSdkRef.Version > hostSdkVersion)
        {
            LogPluginSdkVersionMismatch(
                pluginName,
                pluginSdkRef.Version.ToString(),
                hostSdkVersion.ToString());
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin load failed for {Assembly}: {Message}")]
    private partial void LogPluginReflectionLoadFailed(string assembly, string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Missing dependency: {FileName}")]
    private partial void LogMissingDependency(string fileName);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginName}' was compiled against SDK {PluginSdkVersion}, but host has SDK {HostSdkVersion}. This may cause runtime errors if the plugin uses newer SDK features.")]
    private partial void LogPluginSdkVersionMismatch(string pluginName, string pluginSdkVersion, string hostSdkVersion);
}
