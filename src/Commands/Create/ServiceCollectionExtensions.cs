using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Create.Templates;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Commands.Create;

/// <summary>
/// Extension methods for registering Create command services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Create feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCreateFeature(this IServiceCollection services)
    {
        // Commands
        services.AddTransient<CreateCommand>();
        services.AddTransient<CreatePageCommand>();

        // Core page templates
        services.AddSingleton<IPageTemplate, GalleryPageTemplate>();

        return services;
    }
}
