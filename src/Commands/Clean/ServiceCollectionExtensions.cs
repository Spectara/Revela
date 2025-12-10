using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Commands.Clean;

/// <summary>
/// Extension methods for registering Clean feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Clean feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCleanFeature(this IServiceCollection services)
    {
        services.AddTransient<CleanCommand>();
        return services;
    }
}
