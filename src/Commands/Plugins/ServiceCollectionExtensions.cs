using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Extension methods for registering Plugins feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Plugins feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPluginsFeature(this IServiceCollection services)
    {
        // PluginManager from Core
        services.AddSingleton<PluginManager>();

        // Commands
        services.AddTransient<PluginListCommand>();
        services.AddTransient<PluginInstallCommand>();
        services.AddTransient<PluginUninstallCommand>();
        services.AddTransient<PluginCommand>();

        return services;
    }
}
