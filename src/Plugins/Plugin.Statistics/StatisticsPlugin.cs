using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Plugin.Statistics.Commands;
using Spectara.Revela.Plugin.Statistics.Configuration;

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
        Name = "Statistics",
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
        // Note: ValidateOnStart() removed to avoid DI validation issues
        // Options are validated when first accessed
        services.AddOptions<StatisticsPluginConfig>()
            .BindConfiguration(StatisticsPluginConfig.SectionName)
            .ValidateDataAnnotations();

        // Note: StatisticsAggregator NOT registered here - it depends on IManifestRepository
        // from Commands which would fail DI validation at startup. It's resolved lazily
        // in StatsCommand via IServiceProvider.

        // Note: JsonWriter is now static, no DI needed.

        // Register Commands for Dependency Injection
        services.AddTransient<StatsCommand>();
        services.AddTransient<StatsInitCommand>();
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
        var initCommand = services.GetRequiredService<StatsInitCommand>();

        // 1. Register init command → revela init generate stats
        yield return new CommandDescriptor(initCommand.Create(), ParentCommand: "init generate");

        // 2. Register stats command → revela generate stats
        yield return new CommandDescriptor(statsCommand.Create(), ParentCommand: "generate");
    }
}

/// <summary>
/// Plugin metadata implementation
/// </summary>
internal sealed class PluginMetadata : IPluginMetadata
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
}
