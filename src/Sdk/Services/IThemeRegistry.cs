using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Service for resolving themes with priority: local → installed → default.
/// </summary>
public interface IThemeRegistry
{
    /// <summary>
    /// Resolve a theme by name.
    /// </summary>
    ITheme? Resolve(string? themeName, string projectPath);

    /// <summary>
    /// Resolve an installed theme by name, ignoring local themes.
    /// </summary>
    ITheme? ResolveInstalled(string? themeName);

    /// <summary>
    /// Get all available themes (local + installed).
    /// </summary>
    IEnumerable<ITheme> GetAvailableThemes(string projectPath);

    /// <summary>
    /// Get theme extensions for a specific theme.
    /// </summary>
    IReadOnlyList<ITheme> GetExtensions(string themeName);
}
