using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Features.Theme;

/// <summary>
/// Extension methods for registering Theme feature services.
/// </summary>
public static class ServiceCollectionExtensions
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
        services.AddTransient<ThemeExtractCommand>();

        return services;
    }

    /// <summary>
    /// Registers theme plugins with the DI container.
    /// </summary>
    /// <remarks>
    /// This should be called after plugins are loaded to register
    /// theme plugins for injection into ThemeResolver.
    /// </remarks>
    public static IServiceCollection AddThemePlugins(
        this IServiceCollection services,
        IEnumerable<IThemePlugin> themePlugins)
    {
        foreach (var theme in themePlugins)
        {
            services.AddSingleton(theme);
        }

        return services;
    }
}
