using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class PhotoViewerResolverTests
{
    [TestMethod]
    public void Resolve_PageOverrideWinsAndIsCaseInsensitive()
    {
        // Arrange
        var manifest = CreateManifest(PhotoViewerMode.None, PhotoViewerMode.Page, PhotoViewerMode.Lightbox, PhotoViewerMode.None);

        // Act
        var result = PhotoViewerResolver.Resolve(
            "LiGhTbOx",
            PhotoViewerMode.Page,
            manifest,
            "travel/italy/_index.revela",
            "Lumina");

        // Assert
        Assert.AreEqual(PhotoViewerMode.Lightbox, result);
    }

    [TestMethod]
    public void Resolve_BlankPageOverrideUsesProjectOverride()
    {
        // Arrange
        var manifest = CreateManifest(PhotoViewerMode.None, PhotoViewerMode.Page, PhotoViewerMode.None);

        // Act
        var result = PhotoViewerResolver.Resolve("  ", PhotoViewerMode.Page, manifest, "_index.revela", "Lumina");

        // Assert
        Assert.AreEqual(PhotoViewerMode.Page, result);
    }

    [TestMethod]
    public void Resolve_NoOverridesUsesThemeDefault()
    {
        // Arrange
        var manifest = CreateManifest(PhotoViewerMode.Lightbox, PhotoViewerMode.Page, PhotoViewerMode.Lightbox);

        // Act
        var result = PhotoViewerResolver.Resolve(null, null, manifest, "_index.revela", "Lumina");

        // Assert
        Assert.AreEqual(PhotoViewerMode.Lightbox, result);
    }

    [DataRow("page", PhotoViewerMode.Page)]
    [DataRow("lightbox", PhotoViewerMode.Lightbox)]
    [DataRow("none", PhotoViewerMode.None)]
    [TestMethod]
    public void Resolve_KnownPageMode_ReturnsTypedMode(string rawValue, PhotoViewerMode expected)
    {
        // Arrange
        var manifest = CreateManifest(PhotoViewerMode.None, PhotoViewerMode.Page, PhotoViewerMode.Lightbox, PhotoViewerMode.None);

        // Act
        var result = PhotoViewerResolver.Resolve(rawValue, null, manifest, "gallery/_index.revela", "Lumina");

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Resolve_UnknownPageModeThrowsSourceLocatedDiagnostic()
    {
        // Arrange
        var manifest = CreateManifest(PhotoViewerMode.Page, PhotoViewerMode.Page, PhotoViewerMode.None);

        // Act
        var exception = Assert.ThrowsExactly<PhotoViewerResolutionException>(() =>
            PhotoViewerResolver.Resolve("zoom", null, manifest, "_index.revela", "Lumina"));

        // Assert
        Assert.Contains("_index.revela", exception.Message, StringComparison.Ordinal);
        Assert.Contains("photo_viewer", exception.Message, StringComparison.Ordinal);
        Assert.Contains("zoom", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Lumina", exception.Message, StringComparison.Ordinal);
        Assert.Contains("page, none", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Resolve_UnsupportedPageModeThrowsSourceLocatedDiagnostic()
    {
        // Arrange
        var manifest = CreateManifest(PhotoViewerMode.Page, PhotoViewerMode.Page, PhotoViewerMode.None);

        // Act
        var exception = Assert.ThrowsExactly<PhotoViewerResolutionException>(() =>
            PhotoViewerResolver.Resolve("lightbox", null, manifest, "travel/_index.revela", "Lumina"));

        // Assert
        Assert.Contains("travel/_index.revela", exception.Message, StringComparison.Ordinal);
        Assert.Contains("photo_viewer", exception.Message, StringComparison.Ordinal);
        Assert.Contains("lightbox", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Lumina", exception.Message, StringComparison.Ordinal);
        Assert.Contains("page, none", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Resolve_UnsupportedProjectModeThrowsProjectDiagnostic()
    {
        // Arrange
        var manifest = CreateManifest(PhotoViewerMode.Page, PhotoViewerMode.Page, PhotoViewerMode.None);

        // Act
        var exception = Assert.ThrowsExactly<PhotoViewerResolutionException>(() =>
            PhotoViewerResolver.Resolve(null, PhotoViewerMode.Lightbox, manifest, "_index.revela", "Lumina"));

        // Assert
        Assert.Contains("project.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("theme.photoViewer", exception.Message, StringComparison.Ordinal);
        Assert.Contains("lightbox", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Lumina", exception.Message, StringComparison.Ordinal);
        Assert.Contains("page, none", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Resolve_MissingPhotoViewerCapabilityThrowsArgumentException()
    {
        // Arrange
        var manifest = new ThemeManifest { LayoutTemplate = "layout.revela" };

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() =>
            PhotoViewerResolver.Resolve(null, null, manifest, "_index.revela", "Extension"));
    }

    private static ThemeManifest CreateManifest(
        PhotoViewerMode defaultMode,
        params PhotoViewerMode[] supported) => new()
        {
            LayoutTemplate = "layout.revela",
            PhotoViewer = new PhotoViewerCapabilities
            {
                Default = defaultMode,
                Supported = supported
            }
        };
}
