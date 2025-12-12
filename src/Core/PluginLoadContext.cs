using System.Reflection;
using System.Runtime.Loader;

namespace Spectara.Revela.Core;

/// <summary>
/// Isolated assembly load context for plugins.
/// </summary>
/// <remarks>
/// Each plugin is loaded in its own context, allowing:
/// - Different versions of the same dependency across plugins
/// - No conflicts between plugin dependencies and host
/// - Clean unloading (future feature)
///
/// Shared assemblies (IPlugin interface, Microsoft.Extensions.*) are resolved
/// from the default context to enable cross-context communication.
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver resolver;
    private readonly string dependencyDirectory;

    /// <summary>
    /// Known shared assemblies that should be loaded from the host.
    /// </summary>
    /// <remarks>
    /// These assemblies define contracts between host and plugins.
    /// Loading them from the plugin would break type compatibility.
    /// </remarks>
    private static readonly HashSet<string> SharedAssemblies =
    [
        // Core abstractions (IPlugin, IPluginMetadata, etc.)
        "Spectara.Revela.Core",

        // Commands (shared for plugins that use IManifestRepository, IImageProcessor, etc.)
        "Spectara.Revela.Commands",

        // Microsoft.Extensions.* are shared for DI, Configuration, Logging
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Primitives",

        // System.CommandLine for CLI integration
        "System.CommandLine",

        // Spectre.Console for consistent UI
        "Spectre.Console"
    ];

    public PluginLoadContext(string pluginPath) : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: false)
    {
        resolver = new AssemblyDependencyResolver(pluginPath);

        // Dependencies folder: same name as plugin DLL (without extension)
        var pluginDir = Path.GetDirectoryName(pluginPath)!;
        var pluginName = Path.GetFileNameWithoutExtension(pluginPath);
        dependencyDirectory = Path.Combine(pluginDir, pluginName);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // Shared assemblies: load from host (default context)
        // This ensures IPlugin from plugin == IPlugin from host
        if (IsSharedAssembly(name))
        {
            return null; // Returning null delegates to default context
        }

        // Try resolver first (for NuGet-style layouts)
        var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Try dependency subfolder (our convention: plugins/MyPlugin/*.dll)
        if (Directory.Exists(dependencyDirectory))
        {
            var dependencyPath = Path.Combine(dependencyDirectory, $"{name}.dll");
            if (File.Exists(dependencyPath))
            {
                return LoadFromAssemblyPath(dependencyPath);
            }
        }

        // Not found - delegate to default context
        return null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : 0;
    }

    private static bool IsSharedAssembly(string assemblyName)
    {
        // Exact match
        if (SharedAssemblies.Contains(assemblyName))
        {
            return true;
        }

        // Microsoft.Extensions.* should be shared (except Http which plugins bring)
        // But Http.Resilience needs Polly which is plugin-specific
        if (assemblyName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal))
        {
            // These are plugin-specific (resilience, telemetry, etc.)
            if (assemblyName.Contains("Http", StringComparison.Ordinal) ||
                assemblyName.Contains("Resilience", StringComparison.Ordinal) ||
                assemblyName.Contains("Telemetry", StringComparison.Ordinal) ||
                assemblyName.Contains("Compliance", StringComparison.Ordinal) ||
                assemblyName.Contains("Diagnostics.ExceptionSummarization", StringComparison.Ordinal) ||
                assemblyName.Contains("AmbientMetadata", StringComparison.Ordinal) ||
                assemblyName.Contains("AutoActivation", StringComparison.Ordinal) ||
                assemblyName.Contains("ObjectPool", StringComparison.Ordinal))
            {
                return false; // Plugin-specific
            }

            return true; // Shared
        }

        return false;
    }
}
