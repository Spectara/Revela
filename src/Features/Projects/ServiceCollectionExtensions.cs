using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Features.Projects.Commands;
using Spectara.Revela.Features.Projects.Services;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Projects;

/// <summary>
/// Extension methods for registering Projects feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Projects feature services to the DI container.
    /// </summary>
    public static IServiceCollection AddProjectsFeature(this IServiceCollection services)
    {
        // Project service (UI-free, used by CLI, MCP, GUI)
        services.TryAddTransient<IProjectService, ProjectService>();

        // Commands (thin CLI wrappers)
        services.TryAddTransient<ProjectsCommand>();
        services.TryAddTransient<ProjectsListCommand>();
        services.TryAddTransient<ProjectsCreateCommand>();
        services.TryAddTransient<ProjectsDeleteCommand>();

        return services;
    }
}

