namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Theme plugin interface - extends IPlugin with theme-specific functionality
/// </summary>
/// <remarks>
/// Theme plugins provide:
/// - Template files (Layout.revela, Body/, Partials/)
/// - Static assets (Assets/ folder - CSS, JS, fonts, images)
/// - Theme configuration (variables in theme.json)
///
/// Naming convention: Spectara.Revela.Theme.{Name}
///
/// Usage in project.json:
/// <code>
/// {
///   "theme": "Spectara.Revela.Theme.Lumina"
/// }
/// </code>
///
/// Theme plugins typically don't provide CLI commands, but can
/// register custom Scriban template functions.
/// </remarks>
public interface IThemePlugin : IPlugin
{
    /// <summary>
    /// Theme-specific metadata
    /// </summary>
    new IThemeMetadata Metadata { get; }

    /// <summary>
    /// Get the theme manifest with template and asset information
    /// </summary>
    ThemeManifest GetManifest();

    /// <summary>
    /// Get a file from the theme as a stream
    /// </summary>
    /// <param name="relativePath">Relative path within the theme (e.g., "layout.revela")</param>
    /// <returns>Stream with file contents, or null if not found</returns>
    Stream? GetFile(string relativePath);

    /// <summary>
    /// Get all file paths in the theme
    /// </summary>
    /// <returns>Enumerable of relative paths</returns>
    IEnumerable<string> GetAllFiles();

    /// <summary>
    /// Extract all theme files to a directory
    /// </summary>
    /// <param name="targetDirectory">Directory to extract files to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the site.json template for project initialization
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns a Scriban template for generating site.json during <c>revela init</c>.
    /// The template receives the same model as other init templates (site.title, site.author, etc.).
    /// </para>
    /// <para>
    /// If the theme doesn't need site.json (no site.* variables in templates), return null.
    /// The init command will skip site.json creation in that case.
    /// </para>
    /// </remarks>
    /// <returns>Stream with template contents, or null if theme doesn't use site.json</returns>
    Stream? GetSiteTemplate();

    /// <summary>
    /// Get the images configuration template for image processing setup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns recommended image formats and sizes based on the theme's layout.
    /// Only the theme knows which image sizes make sense for its CSS breakpoints.
    /// </para>
    /// <para>
    /// Expected JSON format:
    /// <code>
    /// {
    ///   "formats": { "webp": 85, "jpg": 90 },
    ///   "sizes": [640, 1024, 1280, 1920, 2560]
    /// }
    /// </code>
    /// Note: AVIF can be added for better compression, but encoding is ~10x slower.
    /// </para>
    /// <para>
    /// If the theme doesn't provide this template, users must enter values manually.
    /// </para>
    /// </remarks>
    /// <returns>Stream with template contents, or null if theme doesn't provide defaults</returns>
    Stream? GetImagesTemplate();
}

/// <summary>
/// Extended metadata for theme plugins
/// </summary>
public interface IThemeMetadata : IPluginMetadata
{
    /// <summary>
    /// URL to preview image of the theme
    /// </summary>
    Uri? PreviewImageUri { get; }

    /// <summary>
    /// Theme tags for discovery (e.g., "minimal", "dark", "gallery")
    /// </summary>
    IReadOnlyList<string> Tags { get; }
}

/// <summary>
/// Theme manifest describing available templates and assets
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>
    /// Main layout template path
    /// </summary>
    public required string LayoutTemplate { get; init; }

    /// <summary>
    /// Theme variables with default values
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Theme extension plugin interface - extends a theme with plugin-specific templates and assets
/// </summary>
/// <remarks>
/// <para>
/// Theme extensions provide templates and CSS for specific plugins, styled for a specific theme.
/// This allows plugins to have beautiful, theme-consistent output without coupling.
/// </para>
///
/// <para>
/// Naming convention: Spectara.Revela.Theme.{ThemeName}.{PluginName}
/// Example: Spectara.Revela.Theme.Lumina.Statistics
/// </para>
///
/// <para>
/// Discovery: Extensions are matched to themes by <see cref="TargetTheme"/> property,
/// no NuGet dependency required. This allows third-party theme extensions.
/// </para>
///
/// <para>
/// Template access: Templates are available as "{PartialPrefix}/{name}" in Scriban.
/// Example: {{ include 'statistics/chart' stats }}
/// </para>
/// </remarks>
public interface IThemeExtension : IPlugin
{
    /// <summary>
    /// Name of the target theme (e.g., "Lumina")
    /// </summary>
    /// <remarks>
    /// Matched case-insensitively against IThemePlugin.Metadata.Name.
    /// Extension only activates when this theme is used.
    /// </remarks>
    string TargetTheme { get; }

    /// <summary>
    /// Prefix for partial templates (e.g., "statistics")
    /// </summary>
    /// <remarks>
    /// Templates are accessed as "{PartialPrefix}/{name}" in Scriban.
    /// Example: "statistics" → {{ include 'statistics/chart' }}
    /// </remarks>
    string PartialPrefix { get; }

    /// <summary>
    /// Get a file from the extension as a stream
    /// </summary>
    /// <param name="relativePath">Relative path within the extension (e.g., "templates/chart.revela")</param>
    /// <returns>Stream with file contents, or null if not found</returns>
    Stream? GetFile(string relativePath);

    /// <summary>
    /// Get all file paths in the extension
    /// </summary>
    /// <returns>Enumerable of relative paths</returns>
    IEnumerable<string> GetAllFiles();

    /// <summary>
    /// Extract all extension files to a directory
    /// </summary>
    /// <param name="targetDirectory">Directory to extract files to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default);
}
