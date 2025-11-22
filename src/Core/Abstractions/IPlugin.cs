using System.CommandLine;
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

    IEnumerable<Command> GetCommands();
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

    /// <summary>
    /// Optional parent command name (e.g., "source", "deploy")
    /// If specified, plugin commands will be registered under this parent command.
    /// If null, commands are registered directly under root.
    /// </summary>
    string? ParentCommand { get; }
}

