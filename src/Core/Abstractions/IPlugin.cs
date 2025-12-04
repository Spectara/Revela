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
    /// Example: configuration.AddJsonFile("onedrive.json", optional: true);
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
/// Theme plugin interface - extends IPlugin with theme-specific functionality
/// </summary>
/// <remarks>
/// Theme plugins provide:
/// - Template files (layout.html, partials, etc.)
/// - Static assets (CSS, JS, fonts, images)
/// - Theme configuration (variables, defaults)
///
/// Naming convention: Spectara.Revela.Theme.{Name}
///
/// Usage in project.json:
/// <code>
/// {
///   "theme": "Spectara.Revela.Theme.Expose"
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
    /// <param name="relativePath">Relative path within the theme (e.g., "layout.html")</param>
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
    /// Partial templates by name (key = include name, value = file path)
    /// </summary>
    public IReadOnlyDictionary<string, string> Partials { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Static assets to copy to output (CSS, JS, fonts, images)
    /// </summary>
    public IReadOnlyList<string> Assets { get; init; } = [];

    /// <summary>
    /// Theme variables with default values
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } =
        new Dictionary<string, string>();
}

