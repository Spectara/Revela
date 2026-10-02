using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate.Services.Checks;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Tests.Commands.Generate.Services.Checks;

/// <summary>
/// Unit tests for <see cref="ConfigCheck"/> — the required site title error and the
/// non-blocking base-URL hint.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ConfigCheckTests
{
    [TestMethod]
    public async Task ValidateAsync_MissingTitle_ReportsError()
    {
        var check = CreateCheck(baseUrl: new Uri("https://example.com"), title: "");

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("title", StringComparison.OrdinalIgnoreCase)),
            "A missing site title must be a blocking error.");
    }

    [TestMethod]
    public async Task ValidateAsync_NoBaseUrl_EmitsHint()
    {
        var check = CreateCheck(baseUrl: null, title: "My Site");

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Hint
                && d.Message.Contains("baseUrl", StringComparison.OrdinalIgnoreCase)),
            "A missing baseUrl must emit a hint.");
        Assert.IsEmpty(diagnostics.Where(d => d.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    public async Task ValidateAsync_TitleAndBaseUrlPresent_ReportsNothing()
    {
        var check = CreateCheck(baseUrl: new Uri("https://example.com"), title: "My Site");

        var diagnostics = await check.ValidateAsync();

        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task ValidateAsync_InvalidThemeConfig_ReportsBindingFailure()
    {
        var themeConfig = Substitute.For<IOptionsMonitor<ThemeConfig>>();
        themeConfig.CurrentValue.Returns(_ => throw new OptionsValidationException(
            Options.DefaultName,
            typeof(ThemeConfig),
            ["theme.photoViewer has an invalid value."]));
        var check = CreateCheck(
            baseUrl: new Uri("https://example.com"),
            title: "My Site",
            themeConfig: themeConfig);

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(diagnostics.Any(d => d.Severity == ValidationSeverity.Error
            && d.Message.Contains("theme.photoViewer", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ValidateAsync_InvalidGenerateConfig_ReportsValidationFailure()
    {
        var generateConfig = Substitute.For<IOptionsMonitor<GenerateConfig>>();
        generateConfig.CurrentValue.Returns(_ => throw new OptionsValidationException(
            Options.DefaultName,
            typeof(GenerateConfig),
            ["Images.AvifEffort must be between 0 and 9."]));
        var check = CreateCheck(
            baseUrl: new Uri("https://example.com"),
            title: "My Site",
            generateConfig: generateConfig);

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(diagnostics.Any(d => d.Severity == ValidationSeverity.Error
            && d.Message.Contains("AvifEffort", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ValidateAsync_UnsupportedProjectViewer_ReportsThemeAndSupportedModes()
    {
        var themeConfig = CreateThemeConfig(new ThemeConfig
        {
            Name = "Limited",
            PhotoViewer = PhotoViewerMode.Page
        });
        var themeRegistry = Substitute.For<IThemeRegistry>();
        var theme = Substitute.For<ITheme>();
        theme.Manifest.Returns(new ThemeManifest
        {
            LayoutTemplate = "Body/Gallery.revela",
            PhotoViewer = new PhotoViewerCapabilities
            {
                Supported = [PhotoViewerMode.Lightbox, PhotoViewerMode.None],
                Default = PhotoViewerMode.Lightbox
            }
        });
        themeRegistry.Resolve("Limited", Arg.Any<string>()).Returns(theme);
        var check = CreateCheck(
            baseUrl: new Uri("https://example.com"),
            title: "My Site",
            themeConfig: themeConfig,
            themeRegistry: themeRegistry);

        var diagnostics = await check.ValidateAsync();

        var diagnostic = diagnostics.Single(d => d.Severity == ValidationSeverity.Error);
        Assert.Contains("theme.photoViewer", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("Limited", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("lightbox, none", diagnostic.Message, StringComparison.Ordinal);
    }

    private static ConfigCheck CreateCheck(
        Uri? baseUrl,
        string title,
        IOptionsMonitor<ThemeConfig>? themeConfig = null,
        IThemeRegistry? themeRegistry = null,
        IOptionsMonitor<GenerateConfig>? generateConfig = null)
    {
        var projectConfig = Substitute.For<IOptionsMonitor<ProjectConfig>>();
        projectConfig.CurrentValue.Returns(new ProjectConfig { Name = "Test", BaseUrl = baseUrl });

        var siteConfig = Substitute.For<IOptionsMonitor<SiteCoreConfig>>();
        siteConfig.CurrentValue.Returns(new SiteCoreConfig { Title = title });

        if (generateConfig is null)
        {
            generateConfig = Substitute.For<IOptionsMonitor<GenerateConfig>>();
            generateConfig.CurrentValue.Returns(new GenerateConfig());
        }

        return new ConfigCheck(
            projectConfig,
            siteConfig,
            themeConfig ?? CreateThemeConfig(new ThemeConfig { Name = "Lumina" }),
            generateConfig,
            themeRegistry ?? Substitute.For<IThemeRegistry>(),
            Options.Create(new ProjectEnvironment { Path = "test-project" }));
    }

    private static IOptionsMonitor<ThemeConfig> CreateThemeConfig(ThemeConfig config)
    {
        var monitor = Substitute.For<IOptionsMonitor<ThemeConfig>>();
        monitor.CurrentValue.Returns(config);
        return monitor;
    }
}
