using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Core.Abstractions;

/// <summary>
/// Plugin interface - all plugins must implement this
/// </summary>
public interface IPlugin
{
    IPluginMetadata Metadata { get; }

    /// <summary>
    /// Configure plugin-specific configuration sources
    /// </summary>
    /// <remarks>
    /// Called BEFORE ConfigureServices to allow plugins to add their own config files.
    /// Use this to register plugin-specific JSON files, environment variables, custom providers, etc.
    /// Example: Framework auto-loads plugins/*.json, plugin can add env vars.
    /// </remarks>
    /// <param name="configuration">Configuration builder to add sources to</param>
    void ConfigureConfiguration(IConfigurationBuilder configuration);

    /// <summary>
    /// Configure services needed by this plugin
    /// </summary>
    /// <remarks>
    /// Called BEFORE ServiceProvider is built.
    /// Use this to register plugin-specific services like HttpClients, database contexts, etc.
    /// </remarks>
    /// <param name="services">The service collection to register services with</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Initialize the plugin with the built ServiceProvider
    /// </summary>
    /// <remarks>
    /// Called AFTER ServiceProvider is built.
    /// Use this to perform initialization that requires resolved services.
    /// </remarks>
    /// <param name="services">The service provider to resolve services from</param>
    void Initialize(IServiceProvider services);

    /// <summary>
    /// Get commands provided by this plugin.
    /// </summary>
    /// <remarks>
    /// Each CommandDescriptor specifies where the command should be registered:
    /// - ParentCommand = null → registered at root level (e.g., "revela mycommand")
    /// - ParentCommand = "init" → registered under init (e.g., "revela init mycommand")
    /// - ParentCommand = "source" → registered under source (e.g., "revela source mycommand")
    /// </remarks>
    /// <returns>Command descriptors with optional parent command information.</returns>
    IEnumerable<CommandDescriptor> GetCommands();
}

/// <summary>
/// Plugin metadata
/// </summary>
public interface IPluginMetadata
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    string Author { get; }
}

/// <summary>
/// Default implementation of <see cref="IPluginMetadata"/>
/// </summary>
/// <remarks>
/// Use this in plugins instead of creating your own implementation.
/// <code>
/// public IPluginMetadata Metadata => new PluginMetadata
/// {
///     Name = "My Plugin",
///     Version = "1.0.0",
///     Description = "Plugin description",
///     Author = "Author Name"
/// };
/// </code>
/// </remarks>
public sealed class PluginMetadata : IPluginMetadata
{
    /// <inheritdoc />
    public required string Name { get; init; }

    /// <inheritdoc />
    public required string Version { get; init; }

    /// <inheritdoc />
    public required string Description { get; init; }

    /// <inheritdoc />
    public required string Author { get; init; }
}

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

