using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Theme.Commands;
using Spectara.Revela.Features.Theme.Services;
using Spectara.Revela.Features.Theme.Wizard;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Theme;

/// <summary>
/// Extension methods for registering Theme feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Theme feature services to the DI container.
    /// </summary>
    public static IServiceCollection AddThemeFeature(this IServiceCollection services)
    {
        // Theme infrastructure
        services.TryAddSingleton<IThemeResolver, ThemeResolver>();

        // Theme service (UI-free, used by CLI commands, MCP, GUI)
        services.TryAddTransient<IThemeService, ThemeService>();

        // Commands (thin CLI wrappers)
        services.TryAddTransient<ThemeCommand>();
        services.TryAddTransient<ThemeListCommand>();
        services.TryAddTransient<ThemeFilesCommand>();
        services.TryAddTransient<ThemeExtractCommand>();
        services.TryAddTransient<ThemeInstallCommand>();
        services.TryAddTransient<ThemeUninstallCommand>();
        services.TryAddTransient<ConfigThemeCommand>();

        // Wizard steps
        services.TryAddEnumerable(ServiceDescriptor.Transient<IWizardStep, ThemeWizardStep>());

        return services;
    }
}

