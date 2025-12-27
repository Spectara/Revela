using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Commands.Clean;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Create;
using Spectara.Revela.Commands.Generate;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Commands.Theme;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Commands;

/// <summary>
/// Extension methods for registering all Revela command services.
/// </summary>
public static class ServiceCollectionExtensions
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

        // Feature commands
        services.AddCleanFeature();
        services.AddConfigFeature();
        services.AddCreateFeature();
        services.AddGenerateFeature();
        services.AddPackagesFeature();
        services.AddPluginsFeature();
        services.AddRestoreFeature();
        services.AddThemeFeature();

        return services;
    }
}
