using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Plugins.Statistics;
using Spectara.Revela.Plugins.Statistics.Commands;
using Spectara.Revela.Plugins.Statistics.Configuration;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Tests.Plugins.Statistics;

[TestClass]
[TestCategory("Unit")]
public sealed class StatisticsPluginRegistrationTests
{
    [TestMethod]
    public void ConfigureServices_InvalidOptions_ThrowsOnAccess()
    {
        // Arrange
        // The [RevelaConfig] source generator enables ValidateDataAnnotations by default,
        // so accessing IOptionsMonitor<T>.CurrentValue with invalid options throws.
        // Validation is deferred to first access (not StartupValidation), so plugins
        // can be installed but unconfigured without crashing the host.
        var services = new ServiceCollection();
        services.AddLogging();

        var manifestRepository = Substitute.For<IManifestRepository>();
        manifestRepository.Images.Returns(new Dictionary<string, ImageContent>());
        manifestRepository.Root.Returns((ManifestEntry?)null);

        services.AddSingleton(manifestRepository);
        services.AddSingleton(TimeProvider.System);

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

        // Act + Assert — first access triggers DataAnnotations validation, which throws.
        var ex = Assert.ThrowsExactly<OptionsValidationException>(() => _ = optionsMonitor.CurrentValue);
        Assert.Contains("MaxEntriesPerCategory", ex.Message);
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
            DataSources = new Dictionary<string, string>()
        });

        services.AddSingleton(manifestRepository);
        services.AddSingleton(TimeProvider.System);

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
