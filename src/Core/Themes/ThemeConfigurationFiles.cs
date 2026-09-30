using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Themes;

/// <summary>
/// Resolves theme configuration files with project-local overrides.
/// </summary>
/// <remarks>
/// Like templates and assets, <c>themes/{ThemeName}/Configuration/images.json</c> in the
/// project overrides the theme's own file. For a local theme this is the theme's file
/// itself; for an installed theme it lets a partial extraction customise image sizes.
/// </remarks>
public static class ThemeConfigurationFiles
{
    /// <summary>
    /// Opens the effective <c>images.json</c>: the project-local override when present,
    /// otherwise <see cref="ITheme.GetImagesTemplate"/>. Returns <see langword="null"/> when neither exists.
    /// </summary>
    public static Stream? OpenImagesTemplate(ITheme theme, string projectPath)
    {
        var overridePath = Path.Combine(projectPath, ProjectPaths.Themes, theme.Metadata.Name, "Configuration", "images.json");
        return File.Exists(overridePath) ? File.OpenRead(overridePath) : theme.GetImagesTemplate();
    }
}
