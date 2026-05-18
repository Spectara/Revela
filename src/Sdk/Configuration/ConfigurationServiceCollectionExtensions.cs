using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Extension methods for registering configuration sections.
/// </summary>
public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Revela configuration sections with IOptionsMonitor support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Binds the following configuration sections to their respective models:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="PackagesConfig"/> - packages section (NuGet feeds)</item>
    /// <item><see cref="DependenciesConfig"/> - dependencies section (theme, plugins)</item>
    /// <item><see cref="GlobalDefaultsConfig"/> - defaults section (default theme)</item>
    /// <item><see cref="GlobalSettingsConfig"/> - settings section (checkUpdates)</item>
    /// <item><see cref="LoggingConfig"/> - Logging section</item>
    /// <item><see cref="ProjectConfig"/> - project section</item>
    /// <item><see cref="ThemeConfig"/> - theme section</item>
    /// <item><see cref="GenerateConfig"/> - generate section</item>
    /// <item><see cref="PathsConfig"/> - paths section (source/output directories)</item>
    /// </list>
    /// <para>
    /// Note: site.json is loaded dynamically by RenderService, not via IOptions.
    /// This allows themes to define custom properties without a fixed schema.
    /// </para>
    /// <para>
    /// All configs support hot-reload via <c>IOptionsMonitor&lt;T&gt;</c>.
    /// </para>
    /// <para>
    /// For writing configuration changes (e.g., adding feeds), use
    /// <see cref="IGlobalConfigManager"/> which writes to revela.json.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRevelaConfigSections(
        this IServiceCollection services)
    {
        // Each AddOptions<T>().BindConfiguration(T.Section) call MUST stay in
        // user-written source so the .NET Configuration Binding Source Generator
        // (EnableConfigurationBindingGenerator=true) can intercept the call site
        // and emit a trim/AOT-safe typed binder. Each Section constant is
        // hand-written on the config class (the source generator's output is
        // invisible to CBSG, which would silently fall back to the reflection
        // binder and break under PublishTrimmed).
        // Configuration is merged from multiple JSON files (revela.json → project.json → logging.json).
        // Hot-reload is provided by BindConfiguration via IOptionsMonitor.
        // Note: site.json is NOT loaded via IOptions — it's loaded dynamically by RenderService.
        services.AddOptions<PackagesConfig>().BindConfiguration(PackagesConfig.Section);
        services.AddOptions<DependenciesConfig>().BindConfiguration(DependenciesConfig.Section);
        services.AddOptions<GlobalDefaultsConfig>().BindConfiguration(GlobalDefaultsConfig.Section);
        services.AddOptions<GlobalSettingsConfig>().BindConfiguration(GlobalSettingsConfig.Section);
        services.AddOptions<LoggingConfig>().BindConfiguration(LoggingConfig.Section);
        services.AddOptions<ProjectConfig>().BindConfiguration(ProjectConfig.Section);
        services.AddOptions<ThemeConfig>().BindConfiguration(ThemeConfig.Section);
        services.AddOptions<GenerateConfig>().BindConfiguration(GenerateConfig.Section);
        services.AddOptions<PathsConfig>().BindConfiguration(PathsConfig.Section);

        // Path resolver service (resolves relative paths against project root)
        // Uses IOptionsMonitor for hot-reload support during interactive sessions
        services.AddSingleton<IPathResolver, PathResolver>();

        // Note: IGlobalConfigManager is registered by Core (requires Core.Services.GlobalConfigManager)
        // Note: ProjectEnvironment is registered in CLI (requires IHostEnvironment)

        return services;
    }
}
