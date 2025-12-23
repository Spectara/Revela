using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Config.Images;
using Spectara.Revela.Commands.Config.Project;
using Spectara.Revela.Commands.Config.Revela;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Commands.Config.Site;
using Spectara.Revela.Commands.Config.Theme;

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

        // Main config command
        services.AddTransient<ConfigCommand>();

        // Project commands
        services.AddTransient<ConfigProjectCommand>();

        // Theme commands
        services.AddTransient<ConfigThemeCommand>();

        // Site commands
        services.AddTransient<ConfigSiteCommand>();

        // Image commands
        services.AddTransient<ConfigImageCommand>();

        // Revela commands (global config)
        services.AddTransient<ConfigFeedCommand>();
        services.AddTransient<ConfigFeedListCommand>();
        services.AddTransient<ConfigFeedAddCommand>();
        services.AddTransient<ConfigFeedRemoveCommand>();
        services.AddTransient<ConfigLocationsCommand>();

        return services;
    }
}
