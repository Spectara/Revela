using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Features.Packages;
using Spectara.Revela.Features.Packages.Commands.Config.Feed;
using Spectara.Revela.Features.Packages.Commands.Packages;
using Spectara.Revela.Features.Packages.Commands.Plugins;
using Spectara.Revela.Features.Packages.Commands.Restore;
using Spectara.Revela.Features.Packages.Commands.Revela;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers NuGet-based package management services and commands.
/// </summary>
/// <remarks>
/// Call from <c>Cli</c> only — <c>Cli.Embedded</c> does not need NuGet
/// because all plugins and themes are statically linked.
/// </remarks>
public static class PackageManagementServiceCollectionExtensions
{
    /// <summary>
    /// Adds package management services, commands, and the <see cref="ICommandProvider"/>
    /// that exposes them to the CLI.
    /// </summary>
    public static IServiceCollection AddPackageManagement(this IServiceCollection services)
    {
        // ── Command provider (ICommandProvider) ──
        services.AddSingleton<ICommandProvider, PackagesCommandProvider>();

        // ── Plugins feature (NuGet services + plugin install/uninstall commands) ──
        services.AddPluginsFeature();

        // ── Packages feature (refresh/search commands) ──
        services.AddPackagesFeature();

        // ── Restore feature ──
        services.AddRestoreFeature();

        // ── Feed config commands (config feed add/remove/list) ──
        services.AddTransient<FeedCommand>();
        services.AddTransient<ListCommand>();
        services.AddTransient<AddCommand>();
        services.AddTransient<RemoveCommand>();

        // ── Setup wizard (first-run interactive mode) ──
        services.AddTransient<Wizard>();
        services.AddTransient<ISetupWizard, Wizard>();

        return services;
    }
}
