using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Features.Generate.Services.Checks;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services.Checks;

[TestClass]
[TestCategory("Unit")]
public sealed class ThemeCheckTests
{
    [TestMethod]
    public async Task ValidateAsync_PageSupportedWithoutPhotoTemplate_ReportsError()
    {
        var check = CreateCheck([PhotoViewerMode.Page], includePhotoTemplate: false);

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(diagnostics.Any(d => d.Severity == ValidationSeverity.Error
            && d.Message.Contains("Body/Photo.revela", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ValidateAsync_PageNotSupportedWithoutPhotoTemplate_DoesNotReportPhotoError()
    {
        var check = CreateCheck([PhotoViewerMode.Lightbox, PhotoViewerMode.None], includePhotoTemplate: false);

        var diagnostics = await check.ValidateAsync();

        Assert.IsFalse(diagnostics.Any(d => d.Message.Contains("Body/Photo.revela", StringComparison.Ordinal)));
    }

    private static ThemeCheck CreateCheck(
        IReadOnlyList<PhotoViewerMode> supported,
        bool includePhotoTemplate)
    {
        var theme = Substitute.For<ITheme>();
        theme.Manifest.Returns(new ThemeManifest
        {
            LayoutTemplate = "body/gallery.revela",
            PhotoViewer = new PhotoViewerCapabilities
            {
                Supported = supported,
                Default = supported[0]
            }
        });

        var themeRegistry = Substitute.For<IThemeRegistry>();
        themeRegistry.Resolve("TestTheme", Arg.Any<string>()).Returns(theme);
        themeRegistry.GetExtensions("TestTheme").Returns([]);

        var templateResolver = Substitute.For<ITemplateResolver>();
        templateResolver.GetTemplate("body/gallery.revela").Returns(_ => new MemoryStream());
        templateResolver.GetTemplate("partials/contentimage.revela").Returns(_ => new MemoryStream());
        if (includePhotoTemplate)
        {
            templateResolver.GetTemplate("body/photo.revela").Returns(_ => new MemoryStream());
        }

        var themeConfig = Substitute.For<IOptionsMonitor<ThemeConfig>>();
        themeConfig.CurrentValue.Returns(new ThemeConfig { Name = "TestTheme" });

        return new ThemeCheck(
            themeRegistry,
            templateResolver,
            Options.Create(new ProjectEnvironment { Path = "test-project" }),
            themeConfig);
    }
}
