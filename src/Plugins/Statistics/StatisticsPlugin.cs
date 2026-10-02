using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Statistics.Commands;
using Spectara.Revela.Plugins.Statistics.Configuration;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Statistics;

/// <summary>
/// Statistics plugin for Revela - generates EXIF statistics pages
/// </summary>
public sealed class StatisticsPlugin : IPlugin
{
    // Statistics data is read by page rendering: run after scan, before pages.
    private const int GenerateOrder = PipelineOrder.Scan + 100;

    // Plugin data is removed after the host's cache clean.
    private const int CleanOrder = CleanPipelineOrder.Cache + 100;

    /// <inheritdoc />
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Statistics",
        Name = "Generate Statistics",
        Version = PackageVersion.FromAssembly(typeof(StatisticsPlugin).Assembly),
        Description = "Generate EXIF statistics pages for your photo library",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // BindConfiguration must live in user-written source so the .NET
        // Configuration Binding Source Generator can intercept it for trim/AOT.
        // The Section constant is hand-written on the config class; the SDK generator
        // reports REVELA002 if it differs from the [RevelaConfig] attribute.
        services.AddOptions<StatisticsPluginConfig>()
            .BindConfiguration(StatisticsPluginConfig.Section);

        // Trim/AOT-safe DataAnnotations validation via the
        // [OptionsValidator] source generator (vs. the reflection-based
        // OptionsBuilder.ValidateDataAnnotations()). TryAddEnumerable keeps it idempotent.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<StatisticsPluginConfig>, StatisticsPluginConfigValidator>());

        services.TryAddTransient<StatisticsAggregator>();
        services.TryAddTransient<StatisticsDataInvalidator>();

        // JsonWriter remains static, no DI needed.

        // Register Commands for Dependency Injection
        services.TryAddTransient<StatsCommand>();
        services.TryAddTransient<CleanStatisticsCommand>();
        services.TryAddTransient<ConfigStatisticsCommand>();

        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IArtifactInvalidator, StatisticsDataInvalidator>());

        // Register as pipeline steps for engine orchestration
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, StatsCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, CleanStatisticsCommand>());

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
        yield return new CommandDescriptor(statsCommand.Create(), ParentCommand: "generate", Order: GenerateOrder, IsSequentialStep: true);

        // Register clean statistics command → revela clean statistics
        yield return new CommandDescriptor(cleanStatsCommand.Create(), ParentCommand: "clean", Order: CleanOrder, IsSequentialStep: true);

        // Register config command → revela config statistics
        yield return new CommandDescriptor(configCommand.Create(), ParentCommand: "config", Group: "Addons");
    }
}
