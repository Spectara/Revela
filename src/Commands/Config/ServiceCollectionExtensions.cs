using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Commands.Config.Feed;
using Spectara.Revela.Commands.Config.Project;
using Spectara.Revela.Commands.Config.Revela;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Commands.Config.Site;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Commands.Config;

/// <summary>
/// Extension methods for registering Config feature services.
/// </summary>
internal static class ServiceCollectionExtensions
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

        // Theme commands moved to Theme Plugin
        // Image, Sorting, Paths commands moved to Generate Plugin

        // Site commands
        services.AddTransient<ConfigSiteCommand>();

        // Feed commands (NuGet sources)
        services.AddTransient<FeedCommand>();
        services.AddTransient<ListCommand>();
        services.AddTransient<AddCommand>();
        services.AddTransient<RemoveCommand>();

        // Revela commands (global config)
        services.AddTransient<ConfigLocationsCommand>();

        return services;
    }
}
