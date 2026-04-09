using Spectara.Revela.Core.Themes;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Default implementation of theme resolver.
/// </summary>
public sealed partial class ThemeResolver(
    IEnumerable<ITheme> installedThemes,
    ILogger<ThemeResolver> logger) : IThemeResolver
{
    private const string DefaultThemeName = "Lumina";

    /// <inheritdoc />
    public IReadOnlyList<ITheme> GetExtensions(string themeName)
    {
        var extensions = installedThemes
            .Where(t => t.TargetTheme is not null
                && t.TargetTheme.Equals(themeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (extensions.Count > 0)
        {
            LogFoundExtensions(logger, themeName, extensions.Count);
        }

        return extensions.AsReadOnly();
    }

    /// <inheritdoc />
    public ITheme? Resolve(string? themeName, string projectPath)
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

        // 2. Check installed themes (base themes only — Prefix is null)
        var installedTheme = installedThemes.FirstOrDefault(
            t => t.Prefix is null
                && t.Metadata.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

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
    public ITheme? ResolveInstalled(string? themeName)
    {
        var name = string.IsNullOrEmpty(themeName) ? DefaultThemeName : themeName;
        LogResolvingTheme(logger, name);

        var installedTheme = installedThemes.FirstOrDefault(
            t => t.Prefix is null
                && t.Metadata.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (installedTheme is not null)
        {
            LogFoundInstalledTheme(logger, name);
            return installedTheme;
        }

        LogThemeNotFound(logger, name);
        return null;
    }

    /// <inheritdoc />
    public IEnumerable<ITheme> GetAvailableThemes(string projectPath)
    {
        List<ITheme> themes = [];
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Local themes (highest priority)
        var localThemes = GetLocalThemes(projectPath);
        foreach (var theme in localThemes)
        {
            themes.Add(theme);
            seenNames.Add(theme.Metadata.Name);
        }

        // 2. Installed base themes only (skip duplicates, skip extensions)
        foreach (var theme in installedThemes.Where(t => t.Prefix is null))
        {
            if (!seenNames.Contains(theme.Metadata.Name))
            {
                themes.Add(theme);
                seenNames.Add(theme.Metadata.Name);
            }
        }

        return themes;
    }

    private LocalThemeProvider? TryResolveLocalTheme(string themeName, string projectPath)
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
            return new LocalThemeProvider(themePath);
        }
        catch (Exception ex)
        {
            LogLocalThemeError(logger, themeName, ex.Message);
            return null;
        }
    }

    private IEnumerable<ITheme> GetLocalThemes(string projectPath)
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

            ITheme? theme = null;
            try
            {
                theme = new LocalThemeProvider(themeDir);
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


