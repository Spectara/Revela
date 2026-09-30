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

    [TestMethod]
    [DataRow(null)]
    [DataRow(/*lang=json,strict*/ """{ "resizeMode": "longest" }""")]
    [DataRow(/*lang=json,strict*/ """{ "sizes": [] }""")]
    [DataRow("""{ "sizes": [ """)]
    public async Task ValidateAsync_ThemeWithoutUsableImageSizes_ReportsError(string? imagesJson)
    {
        var check = CreateCheck([PhotoViewerMode.Page], includePhotoTemplate: true, imagesJson);

        var diagnostics = await check.ValidateAsync();

        var error = diagnostics.Single(d => d.Severity == ValidationSeverity.Error);
        Assert.Contains("Configuration/images.json", error.Message);
        Assert.Contains("themes/TestTheme/Configuration/images.json", error.Suggestion ?? string.Empty);
    }

    [TestMethod]
    public async Task ValidateAsync_ThemeWithImageSizes_ReportsNoError()
    {
        var check = CreateCheck([PhotoViewerMode.Page], includePhotoTemplate: true);

        var diagnostics = await check.ValidateAsync();

        Assert.IsEmpty(diagnostics);
    }

    private static ThemeCheck CreateCheck(
        IReadOnlyList<PhotoViewerMode> supported,
        bool includePhotoTemplate,
        string? imagesJson = /*lang=json,strict*/ """{ "sizes": [640, 1280] }""")
    {
        var theme = Substitute.For<ITheme>();
        theme.Metadata.Returns(new PackageMetadata
        {
            Id = "Test.Theme",
            Name = "TestTheme",
            Version = "1.0.0",
            Description = "Test theme",
            Author = "Test"
        });
        theme.Manifest.Returns(new ThemeManifest
        {
            LayoutTemplate = "body/gallery.revela",
            PhotoViewer = new PhotoViewerCapabilities
            {
                Supported = supported,
                Default = supported[0]
            }
        });
        theme.GetImagesTemplate().Returns(_ => imagesJson is null
            ? null
            : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(imagesJson)));

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
