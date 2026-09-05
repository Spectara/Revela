using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Theme.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Tests.Commands.Theme;

[TestClass]
[TestCategory("Unit")]
public sealed class ThemeServiceTests
{
    [TestMethod]
    public async Task UpdateAsync_ViewerOnly_WritesPartialThemeUpdatePreservingImages()
    {
        var (service, configService, _) = CreateService(
            currentTheme: new ThemeConfig
            {
                Name = "Full",
                Images = new ThemeImagesConfig { Sizes = [640, 1280], ResizeMode = "width" }
            });
        JsonObject? captured = null;
        configService.UpdateProjectConfigAsync(
            Arg.Do<JsonObject>(update => captured = update),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await service.UpdateAsync(new ThemeUpdateRequest(PhotoViewer: PhotoViewerMode.Lightbox));

        Assert.IsTrue(result.Success);
        var themeUpdate = Assert.IsInstanceOfType<JsonObject>(captured?[ThemeConfig.Section]);
        Assert.AreEqual("lightbox", themeUpdate["photoViewer"]?.GetValue<string>());
        Assert.IsFalse(themeUpdate.ContainsKey("name"));
        Assert.IsFalse(themeUpdate.ContainsKey("images"));
    }

    [TestMethod]
    public async Task UpdateAsync_SameThemeWithViewer_WritesBothValues()
    {
        var (service, configService, _) = CreateService(new ThemeConfig { Name = "Full" });
        JsonObject? captured = null;
        configService.UpdateProjectConfigAsync(
            Arg.Do<JsonObject>(update => captured = update),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await service.UpdateAsync(new ThemeUpdateRequest("Full", PhotoViewerMode.Page));

        Assert.IsTrue(result.Success);
        var themeUpdate = Assert.IsInstanceOfType<JsonObject>(captured?[ThemeConfig.Section]);
        Assert.AreEqual("Full", themeUpdate["name"]?.GetValue<string>());
        Assert.AreEqual("page", themeUpdate["photoViewer"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task UpdateAsync_ClearViewer_WritesRemovalWithoutReplacingThemeSection()
    {
        var (service, configService, _) = CreateService(new ThemeConfig
        {
            Name = "Full",
            PhotoViewer = PhotoViewerMode.Page
        });
        JsonObject? captured = null;
        configService.UpdateProjectConfigAsync(
            Arg.Do<JsonObject>(update => captured = update),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await service.UpdateAsync(new ThemeUpdateRequest(ClearPhotoViewer: true));

        Assert.IsTrue(result.Success);
        var themeUpdate = Assert.IsInstanceOfType<JsonObject>(captured?[ThemeConfig.Section]);
        Assert.IsTrue(themeUpdate.ContainsKey("photoViewer"));
        Assert.IsNull(themeUpdate["photoViewer"]);
        Assert.IsFalse(themeUpdate.ContainsKey("images"));
    }

    [TestMethod]
    public async Task UpdateAsync_UnsupportedViewer_FailsWithoutWrite()
    {
        var (service, configService, _) = CreateService(
            new ThemeConfig { Name = "Limited" },
            limitedSupported: [PhotoViewerMode.Lightbox, PhotoViewerMode.None]);

        var result = await service.UpdateAsync(new ThemeUpdateRequest(PhotoViewer: PhotoViewerMode.Page));

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("lightbox, none", result.ErrorMessage, StringComparison.Ordinal);
        await configService.DidNotReceive().UpdateProjectConfigAsync(
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task UpdateAsync_ThemeSwitchWithIncompatibleRetainedViewer_FailsWithoutWrite()
    {
        var (service, configService, _) = CreateService(
            new ThemeConfig { Name = "Full", PhotoViewer = PhotoViewerMode.Page },
            limitedSupported: [PhotoViewerMode.Lightbox]);

        var result = await service.UpdateAsync(new ThemeUpdateRequest(ThemeName: "Limited"));

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("page", result.ErrorMessage, StringComparison.Ordinal);
        await configService.DidNotReceive().UpdateProjectConfigAsync(
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ThemeResults_ResolvedBaseTheme_SurfaceCapabilities()
    {
        var (service, _, capabilities) = CreateService(new ThemeConfig { Name = "Full" });

        var current = service.GetCurrentTheme();
        var list = await service.ListAsync();

        Assert.AreSame(capabilities, current.PhotoViewerCapabilities);
        Assert.HasCount(1, list.Installed);
        Assert.AreSame(capabilities, list.Installed[0].PhotoViewerCapabilities);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GetCurrentTheme_BlankConfiguredName_UsesLumina(string? configuredName)
    {
        var (service, _, capabilities) = CreateService(new ThemeConfig { Name = configuredName! });

        var current = service.GetCurrentTheme();

        Assert.AreEqual("Lumina", current.ThemeName);
        Assert.AreSame(capabilities, current.PhotoViewerCapabilities);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void GetFiles_BlankRequestedName_UsesLumina(string requestedName)
    {
        var (service, _, _) = CreateService(new ThemeConfig { Name = "Full" });

        var files = service.GetFiles(requestedName);

        Assert.AreEqual("Lumina", files.ThemeName);
    }

    [TestMethod]
    public void PhotoViewerCapabilities_ListInput_IsSnapshotted()
    {
        var source = new List<PhotoViewerMode> { PhotoViewerMode.Page };

        var capabilities = new PhotoViewerCapabilities
        {
            Supported = source,
            Default = PhotoViewerMode.Page
        };
        source[0] = PhotoViewerMode.None;

        Assert.AreEqual(PhotoViewerMode.Page, capabilities.Supported[0]);
    }

    private static (ThemeService Service, IConfigService ConfigService, PhotoViewerCapabilities Capabilities) CreateService(
        ThemeConfig currentTheme,
        IReadOnlyList<PhotoViewerMode>? limitedSupported = null)
    {
        var fullCapabilities = new PhotoViewerCapabilities
        {
            Supported = [PhotoViewerMode.Page, PhotoViewerMode.Lightbox, PhotoViewerMode.None],
            Default = PhotoViewerMode.Page
        };
        var fullTheme = CreateTheme("Full", fullCapabilities);
        var limitedTheme = CreateTheme("Limited", new PhotoViewerCapabilities
        {
            Supported = limitedSupported ?? [PhotoViewerMode.Lightbox],
            Default = PhotoViewerMode.Lightbox
        });

        var registry = Substitute.For<IThemeRegistry>();
        registry.Resolve("Full", Arg.Any<string>()).Returns(fullTheme);
        registry.Resolve("Lumina", Arg.Any<string>()).Returns(fullTheme);
        registry.Resolve("Limited", Arg.Any<string>()).Returns(limitedTheme);
        registry.GetAvailableThemes(Arg.Any<string>()).Returns([fullTheme]);
        registry.GetExtensions(Arg.Any<string>()).Returns([]);

        var monitor = Substitute.For<IOptionsMonitor<ThemeConfig>>();
        monitor.CurrentValue.Returns(currentTheme);
        var packageContext = Substitute.For<IPackageContext>();
        packageContext.Themes.Returns([]);
        var configService = Substitute.For<IConfigService>();

        var service = new ThemeService(
            registry,
            Substitute.For<ITemplateResolver>(),
            Substitute.For<IAssetResolver>(),
            packageContext,
            [],
            Substitute.For<IPackageIndexService>(),
            configService,
            Options.Create(new ProjectEnvironment { Path = "test-project" }),
            monitor,
            NullLogger<ThemeService>.Instance);
        return (service, configService, fullCapabilities);
    }

    private static ITheme CreateTheme(string name, PhotoViewerCapabilities capabilities)
    {
        var theme = Substitute.For<ITheme>();
        theme.Metadata.Returns(new PackageMetadata
        {
            Id = $"Spectara.Revela.Themes.{name}",
            Name = name,
            Version = "1.0.0",
            Description = "Test theme"
        });
        theme.Manifest.Returns(new ThemeManifest
        {
            LayoutTemplate = "body/gallery.revela",
            PhotoViewer = capabilities
        });
        return theme;
    }
}
