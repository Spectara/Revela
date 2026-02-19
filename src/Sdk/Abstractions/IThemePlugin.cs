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

/// <summary>
/// Extended metadata for theme plugins — adds preview image and tags.
/// </summary>
/// <remarks>
/// Inherits from <see cref="PluginMetadata"/> (which is a record),
/// so it supports value equality, <c>with</c> expressions, and pattern matching.
/// </remarks>
public record ThemeMetadata : PluginMetadata
{
    /// <summary>URL to preview image of the theme.</summary>
    public Uri? PreviewImageUri { get; init; }

    /// <summary>Theme tags for discovery (e.g., "minimal", "dark", "gallery").</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// Theme manifest describing available templates and assets.
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>Main layout template path.</summary>
    public required string LayoutTemplate { get; init; }

    /// <summary>Theme variables with default values.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Theme extension plugin interface — extends a theme with plugin-specific templates and assets.
/// </summary>
/// <remarks>
/// <para>
/// Theme extensions provide templates and CSS for specific plugins, styled for a specific theme.
/// </para>
/// <para>
/// Naming convention: Spectara.Revela.Theme.{ThemeName}.{PluginName}
/// Example: Spectara.Revela.Theme.Lumina.Statistics
/// </para>
/// <para>
/// Discovery: Extensions are matched to themes by <see cref="TargetTheme"/> property.
/// Template access: Templates are available as "{PartialPrefix}/{name}" in Scriban.
/// </para>
/// </remarks>
public interface IThemeExtension : IPlugin
{
    /// <summary>
    /// Name of the target theme (e.g., "Lumina").
    /// Matched case-insensitively against IThemePlugin.Metadata.Name.
    /// </summary>
    string TargetTheme { get; }

    /// <summary>
    /// Prefix for partial templates (e.g., "statistics").
    /// Templates are accessed as "{PartialPrefix}/{name}" in Scriban.
    /// </summary>
    string PartialPrefix { get; }

    /// <summary>
    /// Extension variables with default values, merged with theme variables.
    /// </summary>
    IReadOnlyDictionary<string, string> Variables { get; }

    /// <summary>
    /// Get default data sources for a template.
    /// </summary>
    /// <param name="templateKey">Template key relative to extension (e.g., "body/overview").</param>
    /// <returns>Dictionary of variable name → default filename, or empty if no defaults.</returns>
    IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey);

    /// <summary>
    /// Get a file from the extension as a stream.
    /// </summary>
    /// <param name="relativePath">Relative path within the extension.</param>
    /// <returns>Stream with file contents, or null if not found.</returns>
    Stream? GetFile(string relativePath);

    /// <summary>
    /// Get all file paths in the extension.
    /// </summary>
    IEnumerable<string> GetAllFiles();

    /// <summary>
    /// Extract all extension files to a directory.
    /// </summary>
    Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default);
}
