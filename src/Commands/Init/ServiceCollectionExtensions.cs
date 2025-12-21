using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Init.Abstractions;
using Spectara.Revela.Commands.Init.Services;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Extension methods for registering Init feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Init feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInitFeature(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<IScaffoldingService, ScaffoldingService>();

        // Commands
        services.AddTransient<InitProjectCommand>();
        services.AddTransient<InitAllCommand>();
        services.AddTransient<InitRevelaCommand>();
        services.AddTransient<InitCommand>();

        return services;
    }
}
