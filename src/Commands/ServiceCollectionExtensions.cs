using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Core.Services;
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
    /// <para>
    /// This is the pre-build phase that registers host-owned services and commands.
    /// Plugin services (Generate, Theme, Projects) are registered by their respective
    /// plugins via <c>IPlugin.ConfigureServices</c> through the plugin loader.
    /// </para>
    /// <para>
    /// Use HostExtensions.UseRevelaCommands() to activate commands post-build.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRevelaCommands(this IServiceCollection services)
    {
        // Shared services
        services.TryAddSingleton<IPackageIndexService, PackageIndexService>();

        // IThemeResolver fallback — registered by Theme Plugin via TryAddSingleton,
        // but needed when plugins aren't loaded (e.g., integration tests)
        services.TryAddSingleton<IThemeResolver, ThemeResolver>();

        // Wizards
        services.AddTransient<RevelaWizard>();
        services.AddTransient<ProjectWizard>();

        // Host-owned commands only (Config, Packages, Plugins, Restore)
        // Plugin commands are registered by plugins via ConfigureServices + GetCommands
        services.AddConfigFeature();
        services.AddPackagesFeature();
        services.AddPluginsFeature();
        services.AddRestoreFeature();

        return services;
    }
}
