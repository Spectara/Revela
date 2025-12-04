using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Features.Restore;

/// <summary>
/// Extension methods for registering Restore feature services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Restore feature services to the container
    /// </summary>
    public static IServiceCollection AddRestoreFeature(this IServiceCollection services)
    {
        services.AddSingleton<IDependencyScanner, DependencyScanner>();
        services.AddTransient<RestoreCommand>();
        return services;
    }
}
