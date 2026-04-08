using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Service for resolving themes with priority: local → plugins → default
/// </summary>
/// <remarks>
/// Theme resolution order:
/// 1. Local theme folder (project/themes/{name}/)
/// 2. Installed theme plugins
/// 3. Default bundled theme (Lumina)
/// </remarks>
public interface IThemeResolver
{
    /// <summary>
    /// Resolve a theme by name
    /// </summary>
    /// <param name="themeName">Theme name (null = default)</param>
    /// <param name="projectPath">Project path for local theme lookup</param>
    /// <returns>Resolved theme plugin or null if not found</returns>
    ITheme? Resolve(string? themeName, string projectPath);

    /// <summary>
    /// Resolve an installed theme by name, ignoring local themes
    /// </summary>
    /// <param name="themeName">Theme name (null = default)</param>
    /// <returns>Installed theme plugin or null if not found</returns>
    ITheme? ResolveInstalled(string? themeName);

    /// <summary>
    /// Get all available themes
    /// </summary>
    /// <param name="projectPath">Project path for local theme lookup</param>
    /// <returns>All available themes (local + installed + default)</returns>
    IEnumerable<ITheme> GetAvailableThemes(string projectPath);

    /// <summary>
    /// Get theme extensions for a specific theme
    /// </summary>
    /// <param name="themeName">Theme name to get extensions for</param>
    /// <returns>List of installed extensions targeting this theme</returns>
    IReadOnlyList<IThemeExtension> GetExtensions(string themeName);
}
