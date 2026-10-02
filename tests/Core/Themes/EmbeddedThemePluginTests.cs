using System.Text.Json.Nodes;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Core.Themes;

/// <summary>
/// Unit tests for <see cref="Sdk.Themes.EmbeddedTheme"/> via <see cref="LuminaTheme"/>
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class EmbeddedThemePluginTests
{
    private LuminaTheme plugin = null!;

    [TestInitialize]
    public void Setup() => plugin = new LuminaTheme();

    [TestMethod]
    public void Metadata_ReturnsCorrectName() => Assert.AreEqual("Lumina", plugin.Metadata.Name);

    [TestMethod]
    public void Metadata_EmbeddedTheme_UsesAssemblyPackageIdentity()
    {
        var metadata = plugin.Metadata;

        Assert.AreEqual("Spectara.Revela.Themes.Lumina", metadata.Id);
        Assert.AreEqual(typeof(LuminaTheme).Assembly.GetName().Name, metadata.Id);
    }

    [TestMethod]
    public void Metadata_ReturnsVersion() => Assert.IsFalse(string.IsNullOrEmpty(plugin.Metadata.Version));

    [TestMethod]
    public void Metadata_EmbeddedTheme_ReportsItsPackageVersion() =>
        Assert.AreEqual(PackageVersion.FromAssembly(typeof(LuminaTheme).Assembly), plugin.Metadata.Version);

    [TestMethod]
    [DataRow(typeof(LuminaTheme))]
    [DataRow(typeof(Revela.Themes.Lumina.Statistics.LuminaStatisticsExtension))]
    [DataRow(typeof(Revela.Themes.Lumina.Calendar.LuminaCalendarExtension))]
    public void Manifest_EmbeddedTheme_DeclaresNoVersionOfItsOwn(Type themeType)
    {
        var theme = (ITheme)Activator.CreateInstance(themeType)!;
        using var stream = theme.GetFile("manifest.json")!;

        var manifest = JsonNode.Parse(stream)!.AsObject();

        Assert.IsFalse(manifest.ContainsKey("version"), "The package version is the theme version; a manifest version drifts.");
    }

    [TestMethod]
    public void GetManifest_ReturnsLayoutTemplate()
    {
        // Arrange & Act
        var manifest = plugin.Manifest;

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(manifest.LayoutTemplate));
    }

    [TestMethod]
    public void GetManifest_ReturnsPhotoViewerCapabilities()
    {
        // Arrange & Act
        var photoViewer = plugin.Manifest.PhotoViewer;

        // Assert
        Assert.IsNotNull(photoViewer);
        CollectionAssert.AreEqual(
            new[] { PhotoViewerMode.Page, PhotoViewerMode.Lightbox, PhotoViewerMode.None },
            photoViewer.Supported.ToArray());
        Assert.AreEqual(PhotoViewerMode.Page, photoViewer.Default);
    }

    [TestMethod]
    public void GetFile_Layout_ReturnsStream()
    {
        // Arrange & Act
        using var stream = plugin.GetFile("Layout.revela");

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetFile_GalleryTemplate_ReturnsStream()
    {
        // Arrange & Act
        using var stream = plugin.GetFile("Body/Gallery.revela");

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetFile_NonExistent_ReturnsNull()
    {
        // Arrange & Act
        var stream = plugin.GetFile("does-not-exist.revela");

        // Assert
        Assert.IsNull(stream);
    }

    [TestMethod]
    public void GetAllFiles_ReturnsMultipleFiles()
    {
        // Arrange & Act
        var files = plugin.GetAllFiles().ToList();

        // Assert
        Assert.IsTrue(files.Count > 5, $"Expected more than 5 files, got {files.Count}");
    }

    [TestMethod]
    public void GetSiteTemplate_ReturnsStream()
    {
        // Arrange & Act
        using var stream = plugin.GetSiteTemplate();

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetImagesTemplate_ReturnsStream()
    {
        // Arrange & Act
        using var stream = plugin.GetImagesTemplate();

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void Metadata_ViaIPackage_ReturnsPackageMetadata()
    {
        // Arrange & Act — access via IPackage interface
        var metadata = plugin.Metadata;

        // Assert
        Assert.AreEqual("Lumina", metadata.Name);
    }

    [TestMethod]
    public async Task ExtractToAsync_ExtractsAllFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"ETP_Test_{Guid.NewGuid():N}");

        try
        {
            // Act
            await plugin.ExtractToAsync(tempDir);

            // Assert
            var extractedFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
            var allFiles = plugin.GetAllFiles().ToList();
            Assert.AreEqual(allFiles.Count, extractedFiles.Length);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [TestMethod]
    [DataRow("Body/Gallery.revela")]
    [DataRow("Body\\Gallery.revela")]
    public void GetFile_EitherPathSeparator_ReturnsStream(string relativePath)
    {
        using var stream = plugin.GetFile(relativePath);

        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetAllFiles_ContainsManifestTemplatesAndStylesheets()
    {
        var files = plugin.GetAllFiles().ToList();

        Assert.IsTrue(files.Any(f => f.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(files.Any(f => f.EndsWith(".revela", StringComparison.OrdinalIgnoreCase)));
        // Lumina ships CSS assets; it intentionally requires no JavaScript (#77).
        Assert.IsTrue(files.Any(f => f.EndsWith(".css", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetAllFiles_ExcludesSourceFiles()
    {
        var files = plugin.GetAllFiles().ToList();

        Assert.IsFalse(files.Any(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task ExtractToAsync_CancelledToken_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ERP_Test_{Guid.NewGuid():N}");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        try
        {
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                plugin.ExtractToAsync(tempDir, cts.Token));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
