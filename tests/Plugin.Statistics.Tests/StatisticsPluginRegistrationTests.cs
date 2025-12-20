using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Plugin.Statistics.Commands;
using Spectara.Revela.Plugin.Statistics.Configuration;
using Spectara.Revela.Plugin.Statistics.Services;

namespace Spectara.Revela.Plugin.Statistics.Tests;

[TestClass]
public sealed class StatisticsPluginRegistrationTests
{
    [TestMethod]
    public void ConfigureServices_InvalidOptions_ShouldThrowOnValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var manifestRepository = Substitute.For<IManifestRepository>();
        manifestRepository.Images.Returns(new Dictionary<string, ImageContent>());
        manifestRepository.Root.Returns((ManifestEntry?)null);

        services.AddSingleton(manifestRepository);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StatisticsPluginConfig.SectionName}:MaxEntriesPerCategory"] = "150"
            }!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        var plugin = new Spectara.Revela.Plugin.Statistics.StatisticsPlugin();
        plugin.ConfigureServices(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<StatisticsPluginConfig>>();

        // Act & Assert
        Assert.ThrowsExactly<OptionsValidationException>(() => optionsMonitor.Get(Options.DefaultName));
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

        var plugin = new Spectara.Revela.Plugin.Statistics.StatisticsPlugin();
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
