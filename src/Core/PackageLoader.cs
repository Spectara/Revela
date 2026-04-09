using System.Reflection;
using System.Runtime.Loader;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Core;

/// <summary>
/// Loads plugins and themes from configured directories.
/// </summary>
/// <remarks>
/// Discovers <see cref="IPlugin"/> and <see cref="ITheme"/> separately.
/// No filtering needed — plugins are plugins, themes are themes.
/// </remarks>
public sealed partial class PackageLoader(
    PackageOptions options,
    ILogger<PackageLoader> logger)
{
    private static readonly string PluginDirectory = ConfigPathResolver.LocalPluginDirectory;
    private static readonly string ApplicationDirectory = AppContext.BaseDirectory;

    private readonly HashSet<string> loadedAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LoadedPluginInfo> loadedPlugins = [];
    private readonly List<LoadedThemeInfo> loadedThemes = [];
    private readonly List<AssemblyLoadContext> pluginContexts = [];

    /// <summary>
    /// Gets the list of loaded plugins.
    /// </summary>
    public IReadOnlyList<LoadedPluginInfo> GetLoadedPlugins() => loadedPlugins.AsReadOnly();

    /// <summary>
    /// Gets the list of loaded theme providers (base themes + extensions).
    /// </summary>
    public IReadOnlyList<LoadedThemeInfo> GetLoadedThemes() => loadedThemes.AsReadOnly();

    /// <summary>
    /// Loads all plugins and themes from configured directories.
    /// </summary>
    public void Load()
    {
        if (options.SearchApplicationDirectory)
        {
            LoadFromDirectory(ApplicationDirectory, "application", PackageSource.Bundled);
        }

        LoadFromDirectory(PluginDirectory, "installed", PackageSource.Local);

        LogPluginsLoaded(loadedPlugins.Count);
        LogThemesLoaded(loadedThemes.Count);
    }

    private void LoadFromDirectory(string directory, string sourceLabel, PackageSource source)
    {
        if (!Directory.Exists(directory))
        {
            LogPluginDirectoryNotFound(directory, sourceLabel);
            return;
        }

        var rootDlls = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
        var subDirDlls = Directory.GetDirectories(directory)
            .Select(subDir =>
            {
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

        var useDefaultContext = sourceLabel == "application";

        foreach (var dll in pluginDlls)
        {
            if (loadedAssemblyPaths.Contains(dll))
            {
                continue;
            }

            var fileName = Path.GetFileName(dll);
            if (options.ExcludePatterns.Any(pattern => MatchesPattern(fileName, pattern)))
            {
                LogPluginExcluded(fileName);
                continue;
            }

            try
            {
                LoadFromAssembly(dll, useDefaultContext, source);
                loadedAssemblyPaths.Add(dll);
            }
            catch (ReflectionTypeLoadException rtle)
            {
                Console.Error.WriteLine(
                    $"Error: Plugin '{fileName}' failed to load: {rtle.Message}");
                foreach (var ex in rtle.LoaderExceptions.Where(e => e != null).Take(3))
                {
                    if (ex is FileNotFoundException fnf && !string.IsNullOrEmpty(fnf.FileName))
                    {
                        Console.Error.WriteLine($"  Missing dependency: {fnf.FileName}");
                        LogMissingDependency(fnf.FileName);
                    }
                }

                LogPluginLoadFailed(rtle, dll);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Error: Plugin '{fileName}' failed to load: {ex.Message}");
                LogPluginLoadFailed(ex, dll);
            }
        }
    }

    private void LoadFromAssembly(string assemblyPath, bool useDefaultContext, PackageSource source)
    {
        var assemblyFileName = Path.GetFileName(assemblyPath);
        Assembly assembly;

        if (useDefaultContext)
        {
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                LogPluginContextCreated(assemblyFileName, "Default (development)");
            }
        }
        else
        {
            var loadContext = new PackageLoadContext(assemblyPath);
            pluginContexts.Add(loadContext);
            assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                LogPluginContextCreated(assemblyFileName, loadContext.Name ?? "unnamed");
            }
        }

        // Discover IPlugin implementations (plugins only, NOT themes)
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in pluginTypes)
        {
            var plugin = (IPlugin?)Activator.CreateInstance(type);
            if (plugin is null)
            {
                continue;
            }

            if (loadedPlugins.Any(p => string.Equals(p.Plugin.Metadata.Id, plugin.Metadata.Id, StringComparison.OrdinalIgnoreCase)))
            {
                LogPluginDuplicate(plugin.Metadata.Name, assemblyPath);
                continue;
            }

            CheckSdkVersionCompatibility(assembly, plugin.Metadata.Name);
            loadedPlugins.Add(new LoadedPluginInfo(plugin, source));

            if (logger.IsEnabled(LogLevel.Information))
            {
                LogPluginDiscovered(plugin.Metadata.Name, plugin.Metadata.Version, assemblyFileName);
            }
        }

        // Discover ITheme implementations (themes and extensions)
        // Only types with parameterless constructors (excludes LocalThemeProvider which needs a path)
        var themeTypes = assembly.GetTypes()
            .Where(t => typeof(ITheme).IsAssignableFrom(t)
                && !t.IsInterface
                && !t.IsAbstract
                && t.GetConstructor(Type.EmptyTypes) is not null);

        foreach (var type in themeTypes)
        {
            var theme = (ITheme?)Activator.CreateInstance(type);
            if (theme is null)
            {
                continue;
            }

            if (loadedThemes.Any(t => string.Equals(t.Theme.Metadata.Id, theme.Metadata.Id, StringComparison.OrdinalIgnoreCase)))
            {
                LogPluginDuplicate(theme.Metadata.Name, assemblyPath);
                continue;
            }

            CheckSdkVersionCompatibility(assembly, theme.Metadata.Name);
            loadedThemes.Add(new LoadedThemeInfo(theme, source));

            if (logger.IsEnabled(LogLevel.Information))
            {
                LogThemeDiscovered(theme.Metadata.Name, theme.Metadata.Version, assemblyFileName);
            }
        }
    }

    private void CheckSdkVersionCompatibility(Assembly pluginAssembly, string pluginName)
    {
        var pluginSdkRef = pluginAssembly.GetReferencedAssemblies()
            .FirstOrDefault(a => string.Equals(a.Name, "Spectara.Revela.Sdk", StringComparison.Ordinal));

        if (pluginSdkRef?.Version is null)
        {
            return;
        }

        var hostSdkAssembly = typeof(IPlugin).Assembly;
        var hostSdkVersion = hostSdkAssembly.GetName().Version;

        if (hostSdkVersion is null)
        {
            return;
        }

        if (pluginSdkRef.Version > hostSdkVersion)
        {
            LogPluginSdkVersionMismatch(pluginName, pluginSdkRef.Version.ToString(), hostSdkVersion.ToString());
        }
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern.StartsWith('*'))
        {
            return fileName.AsSpan().EndsWith(pattern.AsSpan(1), StringComparison.OrdinalIgnoreCase);
        }

        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin directory does not exist: {Directory} ({Source})")]
    private partial void LogPluginDirectoryNotFound(string directory, string source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Searching {Directory} ({Source}): found {Count} plugin candidate(s)")]
    private partial void LogSearchingDirectory(string directory, string source, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin excluded by pattern: {FileName}")]
    private partial void LogPluginExcluded(string fileName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Missing dependency: {FileName}")]
    private partial void LogMissingDependency(string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{Name}' already loaded, skipping duplicate from {Assembly}")]
    private partial void LogPluginDuplicate(string name, string assembly);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load plugin from {Assembly}")]
    private partial void LogPluginLoadFailed(Exception exception, string assembly);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} plugin(s)")]
    private partial void LogPluginsLoaded(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} theme(s)")]
    private partial void LogThemesLoaded(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered plugin: {Name} v{Version} ({Assembly})")]
    private partial void LogPluginDiscovered(string name, string version, string assembly);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered theme: {Name} v{Version} ({Assembly})")]
    private partial void LogThemeDiscovered(string name, string version, string assembly);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created isolated load context '{ContextName}' for plugin {Assembly}")]
    private partial void LogPluginContextCreated(string assembly, string contextName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginName}' was compiled against SDK {PluginSdkVersion}, but host has SDK {HostSdkVersion}. This may cause runtime errors if the plugin uses newer SDK features.")]
    private partial void LogPluginSdkVersionMismatch(string pluginName, string pluginSdkVersion, string hostSdkVersion);
}

