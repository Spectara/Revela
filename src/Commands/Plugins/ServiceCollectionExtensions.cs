using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Extension methods for registering Plugins feature services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Plugins feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPluginsFeature(this IServiceCollection services)
    {
        // NuGetSourceManager for resolving package sources (supports relative paths)
        services.AddSingleton<INuGetSourceManager, NuGetSourceManager>();

        // PluginManager from Core with Typed HttpClient
        // Standard resilience handler provides: retry (3x), circuit breaker, timeout
        services.AddHttpClient<PluginManager>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10); // ZIP downloads can be large
            client.DefaultRequestHeaders.Add("User-Agent", "Revela-PluginManager/1.0");
        })
        .AddStandardResilienceHandler();

        // Commands
        services.AddTransient<PluginListCommand>();
        services.AddTransient<PluginInstallCommand>();
        services.AddTransient<PluginUninstallCommand>();
        services.AddTransient<PluginCommand>();

        return services;
    }
}
