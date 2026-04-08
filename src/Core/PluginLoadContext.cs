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
/// Plugin structure: plugins/{PackageId}/{PackageId}.dll + dependencies
/// All files (main DLL + dependencies) are in the same folder.
///
/// Shared assemblies (IPlugin interface, Microsoft.Extensions.*) are resolved
/// from the default context to enable cross-context communication.
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver resolver;

    /// <summary>
    /// Plugin directory contains main DLL and all dependencies.
    /// </summary>
    private readonly string pluginDirectory;

    public PluginLoadContext(string pluginPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: false)
    {
        resolver = new AssemblyDependencyResolver(pluginPath);
        pluginDirectory = Path.GetDirectoryName(pluginPath)!;
    }

    /// <summary>
    /// Known shared assemblies that should be loaded from the host (exact match).
    /// </summary>
    /// <remarks>
    /// <para>
    /// These assemblies define contracts between host and plugins.
    /// Loading them from the plugin directory would break type compatibility.
    /// </para>
    /// <para>
    /// Additional shared assemblies are detected by naming convention in <see cref="IsSharedAssembly"/>:
    /// <c>Spectara.Revela.*</c>, <c>Microsoft.Extensions.*</c>, <c>Spectre.Console.*</c>.
    /// </para>
    /// <para>
    /// <b>SYNC:</b> These rules must match the exclusion filters in
    /// <c>src/Sdk/build/Spectara.Revela.Sdk.targets</c> (target <c>_RevelaIncludePluginDependencies</c>).
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> SharedAssemblies =
    [
        // System.CommandLine for CLI integration
        "System.CommandLine"
    ];

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
            return LoadFromHost(assemblyName, name);
        }

        // Try resolver first (for NuGet-style layouts)
        var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Try plugin directory (all dependencies are in same folder as main DLL)
        var dependencyPath = Path.Combine(pluginDirectory, $"{name}.dll");
        if (File.Exists(dependencyPath))
        {
            return LoadFromAssemblyPath(dependencyPath);
        }

        // Not found - delegate to default context
        return null;
    }

    /// <summary>
    /// Loads an assembly from the host (default context or already-loaded assemblies).
    /// </summary>
    private static Assembly? LoadFromHost(AssemblyName assemblyName, string name)
    {
        // First, check if already loaded in any context
        var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.Ordinal));

        if (alreadyLoaded is not null)
        {
            return alreadyLoaded;
        }

        // For single-file publish, try to load from default context
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch
        {
            // If not found, return null to let the framework try
            return null;
        }
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : 0;
    }

    /// <summary>
    /// Determines whether an assembly should be loaded from the host (shared)
    /// rather than from the plugin's own directory.
    /// </summary>
    /// <remarks>
    /// SYNC: Keep rules in sync with Sdk/Build/Spectara.Revela.Sdk.targets
    /// which excludes the same assemblies from plugin NuGet packages.
    /// </remarks>
    internal static bool IsSharedAssembly(string assemblyName)
    {
        // Exact match for third-party shared assemblies
        if (SharedAssemblies.Contains(assemblyName))
        {
            return true;
        }

        // Spectre.Console.* are always shared (UI contract)
        if (assemblyName.StartsWith("Spectre.Console", StringComparison.Ordinal))
        {
            return true;
        }

        // Spectara.Revela.* are always shared (Sdk, Core, Commands)
        // This ensures IPlugin, ITheme etc. are the same type in host and plugins
        if (assemblyName.StartsWith("Spectara.Revela.", StringComparison.Ordinal))
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
