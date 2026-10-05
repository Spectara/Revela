using System.Reflection;
using System.Runtime.Loader;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Features.Packages.Services;

/// <summary>
/// Loads plugins and themes from configured directories.
/// </summary>
/// <remarks>
/// Discovers <see cref="IPlugin"/> and <see cref="ITheme"/> separately.
/// No filtering needed — plugins are plugins, themes are themes.
/// </remarks>
internal sealed partial class PackageLoader(
    PackageOptions options,
    ILogger<PackageLoader> logger)
{
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
            LoadFromDirectory(options.ApplicationDirectory, "application", PackageSource.Bundled);
        }

        LoadFromDirectory(options.PluginDirectory, "installed", PackageSource.Local);

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

        var useDefaultContext = source == PackageSource.Bundled;

        // Installed packages always live in plugins/{PackageId}/{PackageId}.dll;
        // only the application directory holds assemblies at its root.
        var pluginDlls = useDefaultContext
            ? Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
            : [.. Directory.GetDirectories(directory)
                .Select(subDir => Path.Combine(subDir, $"{Path.GetFileName(subDir)}.dll"))
                .Where(File.Exists)];

        LogSearchingDirectory(directory, sourceLabel, pluginDlls.Length);

        foreach (var dll in pluginDlls)
        {
            if (loadedAssemblyPaths.Contains(dll))
            {
                continue;
            }

            var fileName = Path.GetFileName(dll);

            try
            {
                LoadFromAssembly(dll, useDefaultContext, source);
                loadedAssemblyPaths.Add(dll);
            }
            catch (ReflectionTypeLoadException rtle)
            {
                var missing = rtle.LoaderExceptions
                    .OfType<FileNotFoundException>()
                    .Select(fnf => fnf.FileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();
                var reason = missing.Count > 0
                    ? $"missing dependency {string.Join(", ", missing)}"
                    : rtle.Message;

                LogPluginLoadFailed(fileName, reason);
                LogPluginLoadFailedDetails(rtle, dll);
            }
            catch (Exception ex)
            {
                LogPluginLoadFailed(fileName, ex.Message);
                LogPluginLoadFailedDetails(ex, dll);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin directory does not exist: {Directory} ({Source})")]
    private partial void LogPluginDirectoryNotFound(string directory, string source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Searching {Directory} ({Source}): found {Count} plugin candidate(s)")]
    private partial void LogSearchingDirectory(string directory, string source, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{Name}' already loaded, skipping duplicate from {Assembly}")]
    private partial void LogPluginDuplicate(string name, string assembly);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Package '{FileName}' was skipped because it failed to load: {Reason}")]
    private partial void LogPluginLoadFailed(string fileName, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading {Assembly} threw")]
    private partial void LogPluginLoadFailedDetails(Exception exception, string assembly);

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

