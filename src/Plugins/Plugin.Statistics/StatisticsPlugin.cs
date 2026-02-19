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
    /// <inheritdoc />
    public PluginMetadata Metadata => new()
    {
        Name = "Generate Statistics",
        Version = "1.0.0",
        Description = "Generate EXIF statistics pages for your photo library",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Plugin Configuration (IOptions pattern)
        // Note: No ValidateDataAnnotations/ValidateOnStart - plugins may be installed but not configured.
        // Validation happens in commands when config is actually needed.
        services.AddOptions<StatisticsPluginConfig>()
            .BindConfiguration(StatisticsPluginConfig.SectionName);

        services.AddTransient<StatisticsAggregator>();

        // JsonWriter remains static, no DI needed.

        // Register Commands for Dependency Injection
        services.AddTransient<StatsCommand>();
        services.AddTransient<CleanStatisticsCommand>();
        services.AddTransient<ConfigStatisticsCommand>();

        // Register StatsCommand as IGenerateStep for pipeline orchestration
        services.AddTransient<IGenerateStep, StatsCommand>();

        // Register Page Template for init commands
        services.AddSingleton<IPageTemplate, StatsPageTemplate>();
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
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
