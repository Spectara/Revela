using Spectara.Revela.Core.Themes;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Default implementation of theme resolver
/// </summary>
public sealed partial class ThemeResolver(
    IEnumerable<IThemePlugin> installedThemes,
    IEnumerable<IThemeExtension> themeExtensions,
    ILogger<ThemeResolver> logger) : IThemeResolver
{
    private const string DefaultThemeName = "Lumina";

    /// <inheritdoc />
    public IReadOnlyList<IThemeExtension> GetExtensions(string themeName)
    {
        var extensions = themeExtensions
            .Where(e => e.TargetTheme.Equals(themeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (extensions.Count > 0)
        {
            LogFoundExtensions(logger, themeName, extensions.Count);
        }

        return extensions.AsReadOnly();
    }

    /// <inheritdoc />
    public IThemePlugin? Resolve(string? themeName, string projectPath)
    {
        var name = string.IsNullOrEmpty(themeName) ? DefaultThemeName : themeName;
        LogResolvingTheme(logger, name);

        // 1. Check local themes folder
        var localTheme = TryResolveLocalTheme(name, projectPath);
        if (localTheme is not null)
        {
            LogFoundLocalTheme(logger, name, projectPath);
            return localTheme;
        }

        // 2. Check installed plugins
        var installedTheme = installedThemes.FirstOrDefault(
            t => t.Metadata.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (installedTheme is not null)
        {
            LogFoundInstalledTheme(logger, name);
            return installedTheme;
        }

        // 3. Not found
        LogThemeNotFound(logger, name);
        return null;
    }

    /// <inheritdoc />
    public IThemePlugin? ResolveInstalled(string? themeName)
    {
        var name = string.IsNullOrEmpty(themeName) ? DefaultThemeName : themeName;
        LogResolvingTheme(logger, name);

        // Only check installed plugins, ignore local themes
        var installedTheme = installedThemes.FirstOrDefault(
            t => t.Metadata.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (installedTheme is not null)
        {
            LogFoundInstalledTheme(logger, name);
            return installedTheme;
        }

        LogThemeNotFound(logger, name);
        return null;
    }

    /// <inheritdoc />
    public IEnumerable<IThemePlugin> GetAvailableThemes(string projectPath)
    {
        List<IThemePlugin> themes = [];
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Local themes (highest priority)
        var localThemes = GetLocalThemes(projectPath);
        foreach (var theme in localThemes)
        {
            themes.Add(theme);
            seenNames.Add(theme.Metadata.Name);
        }

        // 2. Installed plugins (skip duplicates)
        foreach (var theme in installedThemes)
        {
            if (!seenNames.Contains(theme.Metadata.Name))
            {
                themes.Add(theme);
                seenNames.Add(theme.Metadata.Name);
            }
        }

        return themes;
    }

    private LocalThemeAdapter? TryResolveLocalTheme(string themeName, string projectPath)
    {
        var themesPath = Path.Combine(projectPath, ProjectPaths.Themes);
        if (!Directory.Exists(themesPath))
        {
            return null;
        }

        var themePath = Path.Combine(themesPath, themeName);
        if (!Directory.Exists(themePath))
        {
            return null;
        }

        var themeJsonPath = Path.Combine(themePath, "theme.json");
        if (!File.Exists(themeJsonPath))
        {
            LogLocalThemeMissingManifest(logger, themeName, themePath);
            return null;
        }

        try
        {
            return new LocalThemeAdapter(themePath);
        }
        catch (Exception ex)
        {
            LogLocalThemeError(logger, themeName, ex.Message);
            return null;
        }
    }

    private IEnumerable<IThemePlugin> GetLocalThemes(string projectPath)
    {
        var themesPath = Path.Combine(projectPath, ProjectPaths.Themes);
        if (!Directory.Exists(themesPath))
        {
            yield break;
        }

        foreach (var themeDir in Directory.EnumerateDirectories(themesPath))
        {
            var themeJsonPath = Path.Combine(themeDir, "theme.json");
            if (!File.Exists(themeJsonPath))
            {
                continue;
            }

            IThemePlugin? theme = null;
            try
            {
                theme = new LocalThemeAdapter(themeDir);
            }
            catch (Exception ex)
            {
                LogLocalThemeError(logger, Path.GetFileName(themeDir), ex.Message);
            }

            if (theme is not null)
            {
                yield return theme;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolving theme: {ThemeName}")]
    private static partial void LogResolvingTheme(ILogger logger, string themeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found local theme '{ThemeName}' at {Path}")]
    private static partial void LogFoundLocalTheme(ILogger logger, string themeName, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using installed theme: {ThemeName}")]
    private static partial void LogFoundInstalledTheme(ILogger logger, string themeName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Theme '{ThemeName}' not found")]
    private static partial void LogThemeNotFound(ILogger logger, string themeName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Local theme '{ThemeName}' is missing theme.json at {Path}, checking installed themes")]
    private static partial void LogLocalThemeMissingManifest(ILogger logger, string themeName, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load local theme '{ThemeName}': {Error}")]
    private static partial void LogLocalThemeError(ILogger logger, string themeName, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} extension(s) for theme '{ThemeName}'")]
    private static partial void LogFoundExtensions(ILogger logger, string themeName, int count);
}
