using Microsoft.Extensions.DependencyInjection;

namespace Spectara.Revela.Core.Configuration;

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
    /// <see cref="Services.GlobalConfigManager"/> which writes to revela.json.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRevelaConfigSections(
        this IServiceCollection services)
    {
        // All configs use BindConfiguration for proper hot-reload support.
        // BindConfiguration registers IOptionsChangeTokenSource which enables
        // IOptionsMonitor to automatically refresh when configuration files change.
        // Configuration is merged from multiple JSON files (revela.json → project.json → logging.json).
        // Later sources override earlier sources for the same section.
        // Note: site.json is NOT loaded via IOptions - it's loaded dynamically by RenderService.

        // Core sections
        services.AddOptions<ProjectConfig>()
            .BindConfiguration(ProjectConfig.SectionName);

        services.AddOptions<ThemeConfig>()
            .BindConfiguration(ThemeConfig.SectionName);

        services.AddOptions<GenerateConfig>()
            .BindConfiguration(GenerateConfig.SectionName);

        services.AddOptions<DependenciesConfig>()
            .BindConfiguration(DependenciesConfig.SectionName);

        // Global settings
        services.AddOptions<GlobalSettingsConfig>()
            .BindConfiguration(GlobalSettingsConfig.SectionName);

        services.AddOptions<GlobalDefaultsConfig>()
            .BindConfiguration(GlobalDefaultsConfig.SectionName);

        // Package management (NuGet feeds)
        // Standard binding works: { "packages": { "feeds": {...} } } maps to PackagesConfig.Feeds
        services.AddOptions<PackagesConfig>()
            .BindConfiguration(PackagesConfig.SectionName);

        // Logging
        services.AddOptions<LoggingConfig>()
            .BindConfiguration(LoggingConfig.SectionName);

        // Note: ProjectEnvironment is registered in CLI (requires IHostEnvironment)

        return services;
    }
}
