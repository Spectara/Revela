using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugins.Source.OneDrive.Configuration;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Configuration;

/// <summary>
/// Regression tests for <see cref="OneDrivePluginConfig"/> binding/validation behaviour.
/// </summary>
/// <remarks>
/// Guards against a recurring bug: adding <c>[Required]</c>/<c>[Url]</c> annotations to
/// <see cref="OneDrivePluginConfig.ShareUrl"/> causes <see cref="IOptionsMonitor{T}.CurrentValue"/>
/// to throw <see cref="OptionsValidationException"/> the moment any consumer reads the config —
/// which the wizard and <c>ConfigOneDriveCommand</c> do *before* the user has supplied a URL.
/// The interactive callers must be able to observe an empty <c>ShareUrl</c> without exceptions.
/// Required-and-safe URL validation must live at the call site, not on the model.
/// </remarks>
[TestClass]
[TestCategory("Unit")]
public sealed class OneDrivePluginConfigTests
{
    [TestMethod]
    public void CurrentValue_WithoutConfiguredShareUrl_DoesNotThrow()
    {
        // Arrange — empty configuration mirrors a freshly-installed plugin
        // before the user has run `config source onedrive` or the wizard.
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOneDrivePluginConfig();

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<OneDrivePluginConfig>>();

        // Act — this is exactly what OneDriveWizardStep.ShouldPrompt() does.
        var config = monitor.CurrentValue;

        // Assert — empty default, no validation explosion.
        Assert.IsNotNull(config);
        Assert.AreEqual(string.Empty, config.ShareUrl);
    }

    [TestMethod]
    public void CurrentValue_WithConfiguredShareUrl_BindsValue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OneDrivePluginConfig.SectionName}:ShareUrl"] = "https://1drv.ms/f/abc",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOneDrivePluginConfig();

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<OneDrivePluginConfig>>();

        // Act
        var config = monitor.CurrentValue;

        // Assert
        Assert.AreEqual("https://1drv.ms/f/abc", config.ShareUrl);
    }
}
