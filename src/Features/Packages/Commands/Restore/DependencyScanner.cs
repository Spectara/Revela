using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Commands.Restore;

/// <summary>
/// A package required by the merged configuration (<c>dependencies.packages</c>).
/// </summary>
internal sealed record RequiredDependency
{
    /// <summary>
    /// Package identifier (any package ID; the package type is read from the package itself).
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Exact version, or <c>null</c>/<c>"latest"</c> to resolve the newest allowed version.
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
/// Scans configuration for required dependencies
/// </summary>
/// <remarks>
/// Dependencies are read from the merged configuration (revela.json → project.json):
/// <list type="bullet">
/// <item><b>dependencies.packages</b>: required packages (themes, plugins, extensions — any ID)</item>
/// <item><b>theme.name</b>: the active theme's manifest name (resolved via installed themes)</item>
/// </list>
/// </remarks>
internal interface IDependencyScanner
{
    /// <summary>
    /// Gets all packages declared in <c>dependencies.packages</c>.
    /// </summary>
    IReadOnlyList<RequiredDependency> GetDependencies();

    /// <summary>
    /// Gets the configured active theme name (<c>theme.name</c>), or <c>null</c> when not set.
    /// </summary>
    string? GetActiveThemeName();
}

/// <summary>
/// Default implementation of dependency scanner using IOptions
/// </summary>
internal sealed partial class DependencyScanner(
    ILogger<DependencyScanner> logger,
    IOptionsMonitor<DependenciesConfig> options,
    IOptionsMonitor<ThemeConfig> themeOptions) : IDependencyScanner
{
    /// <inheritdoc />
    public IReadOnlyList<RequiredDependency> GetDependencies()
    {
        var dependencies = options.CurrentValue.Packages
            .Select(package => new RequiredDependency { PackageId = package.Key, Version = package.Value })
            .ToList();

        foreach (var dependency in dependencies)
        {
            LogPackageFound(dependency.PackageId, dependency.Version);
        }

        LogDependenciesFound(dependencies.Count);
        return dependencies;
    }

    /// <inheritdoc />
    public string? GetActiveThemeName()
    {
        var name = themeOptions.CurrentValue.Name;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Found package dependency '{PackageId}' version '{Version}'")]
    private partial void LogPackageFound(string packageId, string? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} package dependency(ies)")]
    private partial void LogDependenciesFound(int count);
}
