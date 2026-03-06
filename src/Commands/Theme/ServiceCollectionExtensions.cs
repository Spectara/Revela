using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Extension methods for registering Theme feature services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Theme feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddThemeFeature(this IServiceCollection services)
    {
        // Theme resolver (uses IEnumerable<IThemePlugin> from DI)
        services.AddSingleton<IThemeResolver, ThemeResolver>();

        // Commands
        services.AddTransient<ThemeListCommand>();
        services.AddTransient<ThemeFilesCommand>();
        services.AddTransient<ThemeExtractCommand>();
        services.AddTransient<ThemeInstallCommand>();
        services.AddTransient<ThemeUninstallCommand>();
        services.AddTransient<ThemeCommand>();

        return services;
    }
}
