using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Plugins.Statistics;
using Spectara.Revela.Plugins.Statistics.Commands;
using Spectara.Revela.Plugins.Statistics.Configuration;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Tests.Plugins.Statistics;

[TestClass]
[TestCategory("Unit")]
public sealed class StatisticsPluginRegistrationTests
{
    [TestMethod]
    public void ConfigureServices_RegistersStatisticsDataInvalidator()
    {
        var services = new ServiceCollection();
        var plugin = new StatisticsPlugin();

        plugin.ConfigureServices(services);

        var descriptor = services.SingleOrDefault(item =>
            item.ServiceType == typeof(IArtifactInvalidator));
        Assert.IsNotNull(descriptor);
        Assert.AreEqual(typeof(StatisticsDataInvalidator), descriptor.ImplementationType);
    }

    [TestMethod]
    public void ConfigureServices_CalledTwice_RegistersConfigValidatorOnce()
    {
        var services = new ServiceCollection();
        var plugin = new StatisticsPlugin();

        plugin.ConfigureServices(services);
        plugin.ConfigureServices(services);

        Assert.HasCount(1, services.Where(item => item.ServiceType == typeof(IValidateOptions<StatisticsPluginConfig>)));
    }

    [TestMethod]
    public void ConfigureServices_InvalidOptions_ThrowsOnAccess()
    {
        // Arrange
        // StatisticsPlugin registers a trim/AOT-safe [OptionsValidator]-generated
        // IValidateOptions<StatisticsPluginConfig> alongside the options;
        // accessing IOptionsMonitor<T>.CurrentValue with invalid values
        // triggers DataAnnotations validation on first access (not at startup,
        // so plugins can be installed-but-unconfigured without crashing).
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton(Substitute.For<IManifestReader>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Substitute.For<IArtifactLifecycle>());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StatisticsPluginConfig.Section}:MaxEntriesPerCategory"] = "150" // Invalid: > 100
            }!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Add mock settings writer (required by ConfigStatisticsCommand)
        services.AddSingleton(Substitute.For<IPluginSettingsWriter<StatisticsPluginConfig>>());
        services.AddSingleton(Substitute.For<IConsoleCapabilities>());

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

        services.AddSingleton(Substitute.For<IManifestReader>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Substitute.For<IArtifactLifecycle>());

        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Add mock settings writer (required by ConfigStatisticsCommand)
        services.AddSingleton(Substitute.For<IPluginSettingsWriter<StatisticsPluginConfig>>());
        services.AddSingleton(Substitute.For<IConsoleCapabilities>());

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
