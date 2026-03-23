using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Spectara.Revela.Plugins.Projects.Commands;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Projects;

/// <summary>
/// Project management plugin — list, create, and delete projects.
/// </summary>
public sealed class ProjectsPlugin : IPlugin
{
    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Projects",
        Name = "Projects",
        Version = "1.0.0",
        Description = "Project management (list, create, delete)",
        Author = "Spectara",
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddTransient<ProjectsCommand>();
        services.TryAddTransient<ProjectsListCommand>();
        services.TryAddTransient<ProjectsCreateCommand>();
        services.TryAddTransient<ProjectsDeleteCommand>();
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // Projects only available in standalone mode
        if (!Core.Services.ConfigPathResolver.IsStandaloneMode)
        {
            yield break;
        }

        var projectsCommand = services.GetRequiredService<ProjectsCommand>();
        yield return new CommandDescriptor(
            projectsCommand.Create(),
            Order: 5,
            Group: "Setup",
            RequiresProject: false);
    }
}
