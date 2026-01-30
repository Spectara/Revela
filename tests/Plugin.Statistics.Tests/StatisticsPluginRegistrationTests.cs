using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Plugin.Statistics.Commands;
using Spectara.Revela.Plugin.Statistics.Configuration;
using Spectara.Revela.Plugin.Statistics.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Plugin.Statistics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class StatisticsPluginRegistrationTests
{
    [TestMethod]
    public void ConfigureServices_InvalidOptions_ShouldNotThrowAtStartup()
    {
        // Arrange
        // Note: ValidateDataAnnotations was removed from plugins.
        // Plugins may be installed but not configured - validation happens in commands when config is needed.
        // This test verifies that invalid config does NOT throw at startup anymore.
        var services = new ServiceCollection();
        services.AddLogging();

        var manifestRepository = Substitute.For<IManifestRepository>();
        manifestRepository.Images.Returns(new Dictionary<string, ImageContent>());
        manifestRepository.Root.Returns((ManifestEntry?)null);

        services.AddSingleton(manifestRepository);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StatisticsPluginConfig.SectionName}:MaxEntriesPerCategory"] = "150" // Invalid: > 100
            }!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Add mock IConfigService (required by ConfigStatisticsCommand)
        var configService = Substitute.For<IConfigService>();
        services.AddSingleton(configService);

        var plugin = new StatisticsPlugin();
        plugin.ConfigureServices(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<StatisticsPluginConfig>>();

        // Act - should NOT throw
        var config = optionsMonitor.CurrentValue;

        // Assert - config is bound without validation (value is clamped/handled by command)
        Assert.AreEqual(150, config.MaxEntriesPerCategory);
    }

    [TestMethod]
    public void ConfigureServices_ShouldResolveStatsCommandAndAggregator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var manifestRepository = Substitute.For<IManifestRepository>();
        manifestRepository.Images.Returns(new Dictionary<string, ImageContent>());
        manifestRepository.Root.Returns(new ManifestEntry
        {
            Text = "Root",
            Path = "root",
            DataSources = []
        });

        services.AddSingleton(manifestRepository);

        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Add mock IConfigService (required by ConfigStatisticsCommand)
        var configService = Substitute.For<IConfigService>();
        services.AddSingleton(configService);

        var plugin = new StatisticsPlugin();
        plugin.ConfigureServices(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Act
        var aggregator = provider.GetRequiredService<StatisticsAggregator>();
        var command = provider.GetRequiredService<StatsCommand>();

        // Assert
        Assert.IsNotNull(aggregator);
        Assert.IsNotNull(command);
    }
}
