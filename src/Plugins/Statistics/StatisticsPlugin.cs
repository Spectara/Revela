using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Spectara.Revela.Plugins.Statistics.Commands;
using Spectara.Revela.Plugins.Statistics.Configuration;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Statistics;

/// <summary>
/// Statistics plugin for Revela - generates EXIF statistics pages
/// </summary>
public sealed class StatisticsPlugin : IPlugin
{
    /// <inheritdoc />
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Statistics",
        Name = "Generate Statistics",
        Version = "1.0.0",
        Description = "Generate EXIF statistics pages for your photo library",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Plugin Configuration via SDK helper
        // Binds to project.json section, validates DataAnnotations on access
        // Note: No ValidateOnStart - plugin may be installed but not configured.
        services.AddPluginConfig<StatisticsPluginConfig>();

        services.TryAddTransient<StatisticsAggregator>();

        // JsonWriter remains static, no DI needed.

        // Register Commands for Dependency Injection
        services.TryAddTransient<StatsCommand>();
        services.TryAddTransient<CleanStatisticsCommand>();
        services.TryAddTransient<ConfigStatisticsCommand>();

        // Register StatsCommand as IGenerateStep for pipeline orchestration
        services.TryAddEnumerable(ServiceDescriptor.Transient<IGenerateStep, StatsCommand>());

        // Register CleanStatisticsCommand as ICleanStep for clean pipeline
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICleanStep, CleanStatisticsCommand>());

        // Register Page Template for init commands
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPageTemplate, StatsPageTemplate>());
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
        yield return new CommandDescriptor(statsCommand.Create(), ParentCommand: "generate", Order: 20, IsSequentialStep: true);

        // Register clean statistics command → revela clean statistics
        // Order 30 places it after output (10) and cache (20) in interactive menu
        yield return new CommandDescriptor(cleanStatsCommand.Create(), ParentCommand: "clean", Order: CleanStatisticsCommand.MenuOrder, IsSequentialStep: true);

        // Register config command → revela config statistics
        yield return new CommandDescriptor(configCommand.Create(), ParentCommand: "config", Group: "Addons");
    }
}
