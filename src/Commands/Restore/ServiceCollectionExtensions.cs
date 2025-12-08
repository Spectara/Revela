using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Commands.Restore;

/// <summary>
/// Extension methods for registering Restore feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Restore feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRestoreFeature(this IServiceCollection services)
    {
        services.AddSingleton<IDependencyScanner, DependencyScanner>();
        services.AddTransient<RestoreCommand>();
        return services;
    }
}
