using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Sdk.Abstractions;

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
    /// Note: Plugin configs are typically stored in project.json via IConfigService.
    /// Environment variables with SPECTARA__REVELA__ prefix are auto-loaded.
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
