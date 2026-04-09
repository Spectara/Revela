using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Projects;
using Spectara.Revela.Features.Theme;
using Spectara.Revela.Sdk.Services;

using ProjectWizard = Spectara.Revela.Commands.Project.Wizard;
using RevelaWizard = Spectara.Revela.Commands.Revela.Wizard;

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
    /// Registers both host-owned commands (Config, Packages, Plugins, Restore)
    /// and core features (Generate, Theme, Projects) directly.
    /// External plugins are loaded separately via <c>AddPlugins()</c>.
    /// </remarks>
    public static IServiceCollection AddRevelaCommands(this IServiceCollection services)
    {
        // Shared services
        services.TryAddSingleton<IPackageIndexService, PackageIndexService>();
        services.TryAddSingleton<IThemeResolver, ThemeResolver>();

        // Wizards
        services.AddTransient<RevelaWizard>();
        services.AddTransient<ProjectWizard>();

        // Host-owned commands (Config, Packages, Plugins, Restore)
        services.AddConfigFeature();
        services.AddPackagesFeature();
        services.AddPluginsFeature();
        services.AddRestoreFeature();

        // Core features — always available, not plugin-loaded
        services.AddGenerateFeature();
        services.AddThemeFeature();
        services.AddProjectsFeature();

        return services;
    }
}


