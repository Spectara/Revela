using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Tests.Services;

/// <summary>
/// Tests for <see cref="PluginConfigServiceCollectionExtensions.AddPluginConfig{TConfig}"/>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class PluginConfigServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddPluginConfig_DefaultOptions_RegistersWithoutValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TestPluginConfig.SectionName}:Name"] = "Test"
            }!)
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddPluginConfig<TestPluginConfig>();
        var provider = services.BuildServiceProvider();

        // Assert: Config resolves without validation
        var options = provider.GetRequiredService<IOptions<TestPluginConfig>>();
        Assert.AreEqual("Test", options.Value.Name);
    }

    [TestMethod]
    public void AddPluginConfig_WithValidation_EnablesDataAnnotations()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TestPluginConfig.SectionName}:Name"] = "Valid"
            }!)
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddPluginConfig<TestPluginConfig>(opts => opts.ValidateDataAnnotations = true);
        var provider = services.BuildServiceProvider();

        // Assert: Config resolves with validation enabled
        var options = provider.GetRequiredService<IOptions<TestPluginConfig>>();
        Assert.AreEqual("Valid", options.Value.Name);
    }

    [TestMethod]
    public void AddPluginConfig_WithValidateOnStart_EnablesStartupValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TestPluginConfig.SectionName}:Name"] = "StartupTest"
            }!)
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddPluginConfig<TestPluginConfig>(opts =>
        {
            opts.ValidateDataAnnotations = true;
            opts.ValidateOnStart = true;
        });
        var provider = services.BuildServiceProvider();

        // Assert: Config resolves (validation passes because Name is set)
        var options = provider.GetRequiredService<IOptions<TestPluginConfig>>();
        Assert.AreEqual("StartupTest", options.Value.Name);
    }

    [TestMethod]
    public void AddPluginConfig_NullConfigure_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TestPluginConfig.SectionName}:Name"] = "Default"
            }!)
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Act: Explicit null configure
        services.AddPluginConfig<TestPluginConfig>(null);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<TestPluginConfig>>();
        Assert.AreEqual("Default", options.Value.Name);
    }

    /// <summary>
    /// Test plugin config for verifying AddPluginConfig behavior.
    /// </summary>
    private sealed class TestPluginConfig : IPluginConfig
    {
        public static string SectionName => "TestPlugin";

        [Required]
        public string Name { get; init; } = string.Empty;
    }
}
