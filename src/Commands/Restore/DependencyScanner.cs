using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Commands.Restore;

/// <summary>
/// Represents a required dependency discovered from project configuration
/// </summary>
internal sealed record RequiredDependency
{
    /// <summary>
    /// Package identifier (e.g., "Spectara.Revela.Plugin.Source.OneDrive")
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Optional version constraint (e.g., "1.2.0", ">=1.0.0")
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Type of dependency
    /// </summary>
    public required DependencyType Type { get; init; }
}

/// <summary>
/// Type of dependency
/// </summary>
internal enum DependencyType
{
    /// <summary>Theme package</summary>
    Theme,

    /// <summary>Plugin package</summary>
    Plugin
}

/// <summary>
/// Scans configuration for required dependencies
/// </summary>
/// <remarks>
/// <para>
/// Dependencies are read from the merged configuration (revela.json + project.json).
/// The .NET Configuration system automatically merges these sources.
/// </para>
/// <list type="bullet">
/// <item><b>theme</b>: Active theme (short name or full package ID)</item>
/// <item><b>themes</b>: Installed theme packages with versions</item>
/// <item><b>plugins</b>: Installed plugin packages with versions</item>
/// </list>
/// </remarks>
internal interface IDependencyScanner
{
    /// <summary>
    /// Get all required dependencies from configuration
    /// </summary>
    /// <returns>List of required dependencies</returns>
    IReadOnlyList<RequiredDependency> GetDependencies();
}

/// <summary>
/// Default implementation of dependency scanner using IOptions
/// </summary>
internal sealed partial class DependencyScanner(
    ILogger<DependencyScanner> logger,
    IOptionsMonitor<DependenciesConfig> options) : IDependencyScanner
{
    private const string ThemePackagePrefix = "Spectara.Revela.Theme.";
    private const string PluginPackagePrefix = "Spectara.Revela.Plugin.";

    /// <inheritdoc />
    public IReadOnlyList<RequiredDependency> GetDependencies()
    {
        var config = options.CurrentValue;
        var dependencies = new List<RequiredDependency>();
        var addedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Active theme from "theme" property
        if (!string.IsNullOrEmpty(config.Theme))
        {
            var (name, version) = ParsePackageSpec(config.Theme);
            var packageId = NormalizeThemePackageId(name);

            LogActiveThemeFound(packageId, version);
            dependencies.Add(new RequiredDependency
            {
                PackageId = packageId,
                Version = version,
                Type = DependencyType.Theme
            });
            addedPackageIds.Add(packageId);
        }

        // 2. Themes from "themes" section
        foreach (var (packageId, version) in config.Themes)
        {
            if (!packageId.StartsWith(ThemePackagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                LogInvalidThemePackageId(packageId);
                continue;
            }

            // Skip if already added via "theme" property
            if (!addedPackageIds.Add(packageId))
            {
                continue;
            }

            LogThemeFound(packageId, version);
            dependencies.Add(new RequiredDependency
            {
                PackageId = packageId,
                Version = version,
                Type = DependencyType.Theme
            });
        }

        // 3. Plugins from "plugins" section
        foreach (var (packageId, version) in config.Plugins)
        {
            if (!packageId.StartsWith(PluginPackagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                LogInvalidPluginPackageId(packageId);
                continue;
            }

            LogPluginFound(packageId, version);
            dependencies.Add(new RequiredDependency
            {
                PackageId = packageId,
                Version = version,
                Type = DependencyType.Plugin
            });
        }

        LogDependenciesFound(dependencies.Count);
        return dependencies;
    }

    /// <summary>
    /// Normalize theme name to full package ID
    /// </summary>
    private static string NormalizeThemePackageId(string name) =>
        name.StartsWith(ThemePackagePrefix, StringComparison.OrdinalIgnoreCase)
            ? name
            : ThemePackagePrefix + name;

    /// <summary>
    /// Parse package specification with optional version
    /// </summary>
    /// <example>
    /// "Lumina" → ("Lumina", null)
    /// "Lumina@1.2.0" → ("Lumina", "1.2.0")
    /// </example>
    private static (string Name, string? Version) ParsePackageSpec(string spec)
    {
        var atIndex = spec.LastIndexOf('@');
        if (atIndex > 0 && atIndex < spec.Length - 1)
        {
            return (spec[..atIndex], spec[(atIndex + 1)..]);
        }

        return (spec, null);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Found active theme '{PackageId}' version '{Version}'")]
    private partial void LogActiveThemeFound(string packageId, string? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found theme '{PackageId}' version '{Version}'")]
    private partial void LogThemeFound(string packageId, string? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found plugin '{PackageId}' version '{Version}'")]
    private partial void LogPluginFound(string packageId, string? version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid theme package ID '{PackageId}' (expected 'Spectara.Revela.Theme.*')")]
    private partial void LogInvalidThemePackageId(string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid plugin package ID '{PackageId}' (expected 'Spectara.Revela.Plugin.*')")]
    private partial void LogInvalidPluginPackageId(string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} dependency(ies)")]
    private partial void LogDependenciesFound(int count);
}
