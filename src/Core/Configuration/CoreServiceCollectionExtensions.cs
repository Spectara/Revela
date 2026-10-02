using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Extension methods for registering Core service implementations.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core service implementations (GlobalConfigManager, ConfigFileWriter).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services)
    {
        // Global config manager (revela.json read/write)
        services.AddSingleton<IGlobalConfigManager, GlobalConfigManager>();

        // Atomic JSON config writer shared by every revela.json/project.json/site.json write
        services.TryAddSingleton<ConfigFileWriter>();

        // TimeProvider for testable time abstractions (DateTime.UtcNow replacement)
        services.AddSingleton(TimeProvider.System);

        // Generated artifact dependency coordination
        services.TryAddTransient<IArtifactLifecycle, ArtifactLifecycle>();

        return services;
    }
}
