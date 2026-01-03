using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Provides theme variables from the resolved theme.
/// </summary>
/// <remarks>
/// The theme is already resolved by <see cref="IThemeResolver"/> with priority:
/// 1. Local theme folder (project/themes/{name}/) - via LocalThemeAdapter
/// 2. Installed theme plugins
/// 3. Default bundled theme
///
/// Local themes read their variables from themes/{name}/manifest.json automatically.
/// </remarks>
public interface IThemeVariablesProvider
{
    /// <summary>
    /// Gets the theme variables from the resolved theme.
    /// </summary>
    /// <param name="theme">The resolved theme plugin (may be a local theme adapter).</param>
    /// <returns>Dictionary of variable name to value.</returns>
    IReadOnlyDictionary<string, string> GetVariables(IThemePlugin? theme);
}

/// <summary>
/// Default implementation of <see cref="IThemeVariablesProvider"/>.
/// </summary>
public sealed partial class ThemeVariablesProvider(
    ILogger<ThemeVariablesProvider> logger) : IThemeVariablesProvider
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetVariables(IThemePlugin? theme)
    {
        var themeVariables = theme?.GetManifest().Variables;
        if (themeVariables is not null && themeVariables.Count > 0)
        {
            LogUsingThemeVariables(logger, theme!.Metadata.Name, themeVariables.Count);
            return themeVariables;
        }

        LogNoVariablesFound(logger);
        return new Dictionary<string, string>();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using variables from theme '{ThemeName}' ({Count} variables)")]
    private static partial void LogUsingThemeVariables(ILogger logger, string themeName, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No theme variables found")]
    private static partial void LogNoVariablesFound(ILogger logger);
}
