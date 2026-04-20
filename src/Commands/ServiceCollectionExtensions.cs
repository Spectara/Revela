using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Projects;
using Spectara.Revela.Features.Theme;
using Spectara.Revela.Sdk.Services;

using ProjectWizard = Spectara.Revela.Commands.Project.Wizard;

namespace Spectara.Revela.Commands;

/// <summary>
/// Extension methods for registering all Revela command services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Revela command services to the DI container.
    /// </summary>
    /// <remarks>
    /// Registers host-owned commands (Config) and core features (Generate, Theme, Projects).
    /// Package management commands (Packages, Plugins, Restore) are registered separately
    /// via <c>AddPackageManagement()</c> in the Packages feature — only loaded by <c>Cli</c>,
    /// not by <c>Cli.Embedded</c>.
    /// External plugins are loaded separately via <c>AddPlugins()</c>.
    /// </remarks>
    public static IServiceCollection AddRevelaCommands(this IServiceCollection services)
    {
        // Shared services
        services.TryAddSingleton<IPackageIndexService, PackageIndexService>();
        services.TryAddSingleton<IThemeRegistry, ThemeRegistry>();

        // Wizards (project wizard — setup wizard is in Packages feature)
        services.AddTransient<ProjectWizard>();

        // Host-owned commands
        services.AddConfigFeature();

        // Core features — always available, not plugin-loaded
        services.AddGenerateFeature();
        services.AddThemeFeature();
        services.AddProjectsFeature();

        return services;
    }
}

