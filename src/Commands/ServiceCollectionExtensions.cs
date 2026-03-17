using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Plugins.Generate;

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
    /// This is the pre-build phase that registers all services needed for CLI commands.
    /// Use HostExtensions.UseRevelaCommands() to activate commands post-build.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRevelaCommands(this IServiceCollection services)
    {
        // Shared services
        services.AddSingleton<IPackageIndexService, PackageIndexService>();

        // IThemeResolver fallback — normally registered by Theme Plugin,
        // but needed when plugins aren't loaded (e.g., integration tests)
        services.TryAddSingleton<IThemeResolver, ThemeResolver>();

        // Wizards
        services.AddTransient<RevelaWizard>();
        services.AddTransient<ProjectWizard>();

        // Host commands (plugins register their own via ConfigureServices + GetCommands)
        // Generate Plugin: generate, clean, create, config images/sorting/paths + IThemeResolver
        // Theme Plugin: theme, config theme
        // Projects Plugin: projects
        services.AddGenerateFeature();
        services.AddConfigFeature();
        services.AddPackagesFeature();
        services.AddPluginsFeature();
        services.AddRestoreFeature();

        return services;
    }
}
