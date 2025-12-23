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
    /// <remarks>
    /// Only registers scaffolding service. Init commands have been migrated to config commands.
    /// </remarks>
    public static IServiceCollection AddInitFeature(this IServiceCollection services)
    {
        // Services (still needed by config commands)
        services.AddSingleton<IScaffoldingService, ScaffoldingService>();

        return services;
    }
}
