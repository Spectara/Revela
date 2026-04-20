using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Commands.Packages;

/// <summary>
/// Extension methods for registering Packages feature services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Packages feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPackagesFeature(this IServiceCollection services)
    {
        // Main packages command
        services.AddTransient<PackagesCommand>();

        // Subcommands - RefreshCommand needs HttpClient for NuGet Search API
        services.AddHttpClient<RefreshCommand>();
        services.AddTransient<SearchCommand>();

        return services;
    }
}
