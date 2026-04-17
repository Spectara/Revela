using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Extension methods for registering Core service implementations.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core service implementations (GlobalConfigManager).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services)
    {
        // Global config manager (revela.json read/write)
        services.AddSingleton<IGlobalConfigManager, GlobalConfigManager>();

        // TimeProvider for testable time abstractions (DateTime.UtcNow replacement)
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
