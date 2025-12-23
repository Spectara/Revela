using Microsoft.Extensions.Configuration;
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
    /// <item><see cref="FeedsConfig"/> - feeds (NuGet feeds, merged)</item>
    /// <item><see cref="DependenciesConfig"/> - dependencies section (theme, plugins)</item>
    /// <item><see cref="GlobalDefaultsConfig"/> - defaults section (default theme)</item>
    /// <item><see cref="GlobalSettingsConfig"/> - settings section (checkUpdates)</item>
    /// <item><see cref="LoggingConfig"/> - Logging section</item>
    /// <item><see cref="ProjectConfig"/> - project section</item>
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
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRevelaConfigSections(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // All configs use section-based binding with IOptionsMonitor support.
        // Configuration is merged from multiple JSON files (revela.json → project.json → logging.json).
        // Later sources override earlier sources for the same section.
        // Note: site.json is NOT loaded via IOptions - it's loaded dynamically by RenderService.

        // Core sections
        services.AddOptions<ProjectConfig>()
            .Bind(configuration.GetSection(ProjectConfig.SectionName));

        services.AddOptions<GenerateConfig>()
            .Bind(configuration.GetSection(GenerateConfig.SectionName));

        services.AddOptions<DependenciesConfig>()
            .Bind(configuration.GetSection(DependenciesConfig.SectionName));

        // Global settings
        services.AddOptions<GlobalSettingsConfig>()
            .Bind(configuration.GetSection(GlobalSettingsConfig.SectionName));

        services.AddOptions<GlobalDefaultsConfig>()
            .Bind(configuration.GetSection(GlobalDefaultsConfig.SectionName));

        // NuGet feeds
        services.AddOptions<FeedsConfig>()
            .Bind(configuration.GetSection(FeedsConfig.SectionName));

        // Logging
        services.AddOptions<LoggingConfig>()
            .Bind(configuration.GetSection(LoggingConfig.SectionName));

        return services;
    }
}
