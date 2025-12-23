using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Plugin.Statistics.Commands;
using Spectara.Revela.Plugin.Statistics.Configuration;
using Spectara.Revela.Plugin.Statistics.Services;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugin.Statistics;

/// <summary>
/// Statistics plugin for Revela - generates EXIF statistics pages
/// </summary>
public sealed class StatisticsPlugin : IPlugin
{
    private IServiceProvider? services;

    /// <inheritdoc />
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "Generate Statistics",
        Version = "1.0.0",
        Description = "Generate EXIF statistics pages for your photo library",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Nothing to do - framework handles all configuration:
        // - JSON files: auto-loaded from plugins/*.json
        // - ENV vars: auto-loaded with SPECTARA__REVELA__ prefix
        //
        // Example ENV: SPECTARA__REVELA__PLUGIN__STATISTICS__OUTPUTPATH=source/stats
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Plugin Configuration (IOptions pattern)
        services.AddOptions<StatisticsPluginConfig>()
            .BindConfiguration(StatisticsPluginConfig.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddTransient<StatisticsAggregator>();

        // JsonWriter remains static, no DI needed.

        // Register Commands for Dependency Injection
        services.AddTransient<StatsCommand>();
        services.AddTransient<CleanStatisticsCommand>();
        services.AddTransient<ConfigStatisticsCommand>();

        // Register Page Template for init commands
        services.AddSingleton<IPageTemplate, StatsPageTemplate>();
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider services) => this.services = services;

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands()
    {
        if (services is null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call Initialize() first.");
        }

        // Resolve commands from DI container
        var statsCommand = services.GetRequiredService<StatsCommand>();
        var cleanStatsCommand = services.GetRequiredService<CleanStatisticsCommand>();
        var configCommand = services.GetRequiredService<ConfigStatisticsCommand>();

        // Register stats command → revela generate statistics
        // Order 20 places it between scan (10) and pages (30) in interactive menu
        yield return new CommandDescriptor(statsCommand.Create(), ParentCommand: "generate", Order: 20);

        // Register clean statistics command → revela clean statistics
        // Order 30 places it after output (10) and cache (20) in interactive menu
        yield return new CommandDescriptor(cleanStatsCommand.Create(), ParentCommand: "clean", Order: CleanStatisticsCommand.Order);

        // Register config command → revela config statistics
        yield return new CommandDescriptor(configCommand.Create(), ParentCommand: "config");
    }
}
