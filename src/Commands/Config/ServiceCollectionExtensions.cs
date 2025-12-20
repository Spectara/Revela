using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Config.Services;

namespace Spectara.Revela.Commands.Config;

/// <summary>
/// Extension methods for registering Config feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Config feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConfigFeature(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<IConfigService, ConfigService>();

        // Commands
        services.AddTransient<ConfigCommand>();
        services.AddTransient<ConfigThemeCommand>();
        services.AddTransient<ConfigSiteCommand>();
        services.AddTransient<ConfigImagesCommand>();
        services.AddTransient<ConfigShowCommand>();

        return services;
    }
}
