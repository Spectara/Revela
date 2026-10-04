using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Plugin interface — all plugins must implement this.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ConfigureServices"/> registers the plugin's services with DI. After the host
/// is built, <see cref="GetCommands"/> is called with the built <see cref="IServiceProvider"/>
/// so plugins can resolve commands from DI.
/// </para>
/// <para>
/// Configuration sources belong to the host. A plugin reads its own settings through
/// <c>IOptions&lt;T&gt;</c> of its <see cref="RevelaConfigAttribute"/> type and writes them
/// through <c>IPluginSettingsWriter&lt;T&gt;</c>.
/// </para>
/// </remarks>
public interface IPlugin : IPackage
{
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
