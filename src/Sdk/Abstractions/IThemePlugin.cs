namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Theme plugin interface — extends IPlugin with theme-specific functionality.
/// </summary>
/// <remarks>
/// Theme plugins provide:
/// <list type="bullet">
/// <item>Template files (Layout.revela, Body/, Partials/)</item>
/// <item>Static assets (Assets/ folder — CSS, JS, fonts, images)</item>
/// <item>Theme configuration (variables in manifest.json)</item>
/// </list>
///
/// Naming convention: Spectara.Revela.Theme.{Name}
///
/// Usage in project.json:
/// <code>
/// { "theme": "Spectara.Revela.Theme.Lumina" }
/// </code>
///
/// Theme plugins typically don't provide CLI commands, but can
/// register custom Scriban template functions.
/// </remarks>
public interface IThemePlugin : IPlugin
{
    /// <summary>
    /// Theme-specific metadata with preview image and tags.
    /// </summary>
    new ThemeMetadata Metadata { get; }

    /// <summary>
    /// Get the theme manifest with template and asset information.
    /// </summary>
    ThemeManifest GetManifest();

    /// <summary>
    /// Get a file from the theme as a stream.
    /// </summary>
    /// <param name="relativePath">Relative path within the theme (e.g., "layout.revela").</param>
    /// <returns>Stream with file contents, or null if not found.</returns>
    Stream? GetFile(string relativePath);

    /// <summary>
    /// Get all file paths in the theme.
    /// </summary>
    /// <returns>Enumerable of relative paths.</returns>
    IEnumerable<string> GetAllFiles();

    /// <summary>
    /// Extract all theme files to a directory.
    /// </summary>
    /// <param name="targetDirectory">Directory to extract files to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the site.json template for project initialization.
    /// </summary>
    /// <remarks>
    /// Returns a Scriban template for generating site.json during <c>revela init</c>.
    /// If the theme doesn't need site.json, return null.
    /// </remarks>
    /// <returns>Stream with template contents, or null if theme doesn't use site.json.</returns>
    Stream? GetSiteTemplate();

    /// <summary>
    /// Get the images configuration template for image processing setup.
    /// </summary>
    /// <remarks>
    /// Returns recommended image formats and sizes based on the theme's CSS breakpoints.
    /// If the theme doesn't provide this template, users must enter values manually.
    /// </remarks>
    /// <returns>Stream with template contents, or null if theme doesn't provide defaults.</returns>
    Stream? GetImagesTemplate();
}
