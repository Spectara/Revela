using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Commands.Projects.Commands;

namespace Spectara.Revela.Commands.Projects;

/// <summary>
/// Extension methods for registering project management services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds project management commands to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProjectsFeature(this IServiceCollection services)
    {
        services.AddTransient<ProjectsCommand>();
        services.AddTransient<ProjectsListCommand>();
        services.AddTransient<ProjectsCreateCommand>();
        services.AddTransient<ProjectsDeleteCommand>();

        return services;
    }
}
