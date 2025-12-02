using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Features.Generate;

/// <summary>
/// Extension methods for registering Generate feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Generate feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGenerateFeature(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<ExifCache>();
        services.AddSingleton<IImageProcessor, NetVipsImageProcessor>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
        services.AddSingleton<ContentScanner>();
        services.AddSingleton<SiteGenerator>();

        // Commands
        services.AddTransient<GenerateCommand>();

        return services;
    }
}
