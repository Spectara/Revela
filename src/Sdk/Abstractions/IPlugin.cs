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
/// </remarks>
public interface IPlugin : IPackage
{
    /// <summary>
    /// Configure plugin-specific configuration sources (optional).
    /// </summary>
    /// <param name="configuration">Configuration builder to add sources to.</param>
    void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Default: no custom configuration sources needed
    }

    /// <summary>
    /// Configure services needed by this plugin.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Get commands provided by this plugin (optional).
    /// </summary>
    /// <param name="services">The built service provider to resolve commands from.</param>
    /// <returns>Command descriptors with optional parent command information.</returns>
    IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services) => [];
}
