using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Plugin interface — all plugins must implement this.
/// </summary>
/// <remarks>
/// <para>
/// Plugins have a simple 2-phase lifecycle:
/// </para>
/// <list type="number">
/// <item><see cref="ConfigureConfiguration"/> — Register custom config sources (optional, default: no-op)</item>
/// <item><see cref="ConfigureServices"/> — Register services with DI (required)</item>
/// </list>
/// <para>
/// After the host is built, <see cref="GetCommands"/> is called with the built
/// <see cref="IServiceProvider"/> so plugins can resolve commands from DI.
/// </para>
/// <example>
/// <code>
/// public sealed class MyPlugin : IPlugin
/// {
///     public PluginMetadata Metadata { get; } = new()
///     {
///         Name = "My Plugin",
///         Version = "1.0.0",
///         Description = "Does something useful"
///     };
///
///     public void ConfigureServices(IServiceCollection services)
///     {
///         services.AddTransient&lt;MyCommand&gt;();
///     }
///
///     public IEnumerable&lt;CommandDescriptor&gt; GetCommands(IServiceProvider services)
///     {
///         var cmd = services.GetRequiredService&lt;MyCommand&gt;();
///         yield return new CommandDescriptor(cmd.Create(), Order: 10, Group: "Build");
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public interface IPlugin
{
    /// <summary>
    /// Gets the plugin metadata (name, version, description, author).
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Configure plugin-specific configuration sources (optional).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called BEFORE <see cref="ConfigureServices"/>. Override only if your plugin needs
    /// custom configuration sources beyond what the framework provides automatically:
    /// </para>
    /// <list type="bullet">
    /// <item>project.json section: <c>"Spectara.Revela.Plugins.YourPlugin": { ... }</c></item>
    /// <item>Environment variables with <c>SPECTARA__REVELA__</c> prefix, where the remaining
    /// key path matches the full section name using <c>__</c> as separator, e.g.:
    /// <c>SPECTARA__REVELA__SPECTARA.REVELA.PLUGINS.YOURPLUGIN__SETTINGNAME=value</c></item>
    /// </list>
    /// <para>
    /// Examples of when to override: loading config from a plugin-specific file,
    /// connecting to an external configuration service, or adding computed defaults.
    /// </para>
    /// </remarks>
    /// <param name="configuration">Configuration builder to add sources to.</param>
    void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Default: no custom configuration sources needed
    }

    /// <summary>
    /// Configure services needed by this plugin.
    /// </summary>
    /// <remarks>
    /// Called BEFORE ServiceProvider is built.
    /// Use this to register plugin-specific services like HttpClients, commands, IOptions, etc.
    /// </remarks>
    /// <param name="services">The service collection to register services with.</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Get commands provided by this plugin (optional).
    /// </summary>
    /// <remarks>
    /// Called AFTER ServiceProvider is built. Use <paramref name="services"/> to resolve
    /// command instances from DI. Each <see cref="CommandDescriptor"/> specifies where
    /// the command should be registered in the CLI tree:
    /// <list type="bullet">
    /// <item>ParentCommand = null → root level (e.g., "revela mycommand")</item>
    /// <item>ParentCommand = "source" → nested (e.g., "revela source mycommand")</item>
    /// </list>
    /// </remarks>
    /// <param name="services">The built service provider to resolve commands from.</param>
    /// <returns>Command descriptors with optional parent command information.</returns>
    IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services) => [];
}

/// <summary>
/// Plugin metadata — name, version, description, and author.
/// </summary>
/// <remarks>
/// Use directly in plugins:
/// <code>
/// public PluginMetadata Metadata { get; } = new()
/// {
///     Name = "My Plugin",
///     Version = "1.0.0",
///     Description = "Plugin description",
///     Author = "Author Name"
/// };
/// </code>
/// </remarks>
public record PluginMetadata
{
    /// <summary>Fully qualified plugin package ID (e.g., "Spectara.Revela.Plugins.Core.Generate").</summary>
    /// <remarks>
    /// Used for dependency resolution (<see cref="RequiredPlugins"/>, <see cref="ExtendsPlugins"/>)
    /// and duplicate detection. Must be globally unique.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>Plugin display name (short, human-readable).</summary>
    public required string Name { get; init; }

    /// <summary>Plugin version (semver).</summary>
    public required string Version { get; init; }

    /// <summary>Brief description of the plugin.</summary>
    public required string Description { get; init; }

    /// <summary>Plugin author or organization.</summary>
    public string Author { get; init; } = "Unknown";

    /// <summary>
    /// Fully qualified plugin IDs (<see cref="Id"/>) that MUST be installed for this plugin to work.
    /// The host validates these before loading the plugin.
    /// </summary>
    /// <example>
    /// <code>
    /// RequiredPlugins = ["Spectara.Revela.Plugins.Core.Generate"]
    /// </code>
    /// </example>
    public IReadOnlyList<string> RequiredPlugins { get; init; } = [];

    /// <summary>
    /// Fully qualified plugin IDs (<see cref="Id"/>) that this plugin optionally extends.
    /// Extension commands are only registered if the target plugin is present.
    /// </summary>
    /// <example>
    /// <code>
    /// ExtendsPlugins = ["Spectara.Revela.Plugins.Core.Generate"]
    /// </code>
    /// </example>
    public IReadOnlyList<string> ExtendsPlugins { get; init; } = [];
}
