using System.Text.Json;

using Spectara.Revela.Core.Themes;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Themes;

namespace Spectara.Revela.Tests.Core.Themes;

/// <summary>
/// Unit tests for <see cref="LocalThemeProvider"/>
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class LocalThemeProviderTests
{
    private string tempDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"LTA_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Constructor_ValidThemeJson_SetsMetadata()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("MyTheme");

        // Act
        var adapter = new LocalThemeProvider(themeDir);

        // Assert
        Assert.AreEqual("MyTheme", adapter.Metadata.Name);
        Assert.AreEqual("1.0.0", adapter.Metadata.Version);
    }

    [TestMethod]
    public void Constructor_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var nonExistent = Path.Combine(tempDirectory, "non-existent");

        // Act & Assert
        Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => new LocalThemeProvider(nonExistent));
    }

    [TestMethod]
    public void Constructor_MissingThemeJson_ThrowsFileNotFoundException()
    {
        // Arrange — directory exists but no theme.json
        var emptyDir = Path.Combine(tempDirectory, "empty-theme");
        Directory.CreateDirectory(emptyDir);

        // Act & Assert
        Assert.ThrowsExactly<FileNotFoundException>(
            () => new LocalThemeProvider(emptyDir));
    }

    [TestMethod]
    [DataRow("Lumina", null)]
    [DataRow(null, "calendar")]
    [DataRow("Lumina", "calendar")]
    public void Constructor_ExtensionMetadata_ThrowsThemeJsonDiagnostic(
        string? targetTheme,
        string? prefix)
    {
        var themeDir = CreateThemeDirectory(
            "TestTheme",
            configure: config =>
            {
                config.TargetTheme = targetTheme;
                config.Prefix = prefix;
            });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => new LocalThemeProvider(themeDir));

        StringAssert.Contains(exception.Message, "theme.json", StringComparison.Ordinal);
    }

    [TestMethod]
    public void GetManifest_ReturnsLayoutTemplate()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme", layout: "Custom.revela");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        var manifest = adapter.Manifest;

        // Assert
        Assert.AreEqual("Custom.revela", manifest.LayoutTemplate);
    }

    [TestMethod]
    public void GetManifest_DefaultLayout_UsesLayoutRevela()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        var manifest = adapter.Manifest;

        // Assert
        Assert.AreEqual("layout.revela", manifest.LayoutTemplate);
    }

    [TestMethod]
    public void GetManifest_CaseInsensitivePhotoViewerValues_MapsCapabilities()
    {
        // Arrange
        var themeDir = CreateThemeDirectory(
            "TestTheme",
            configure: config =>
            {
                config.PhotoViewers = ["PAGE", "LightBox", "none"];
                config.DefaultPhotoViewer = "pAgE";
            });

        // Act
        var photoViewer = new LocalThemeProvider(themeDir).Manifest.PhotoViewer;

        // Assert
        Assert.IsNotNull(photoViewer);
        CollectionAssert.AreEqual(
            new[] { PhotoViewerMode.Page, PhotoViewerMode.Lightbox, PhotoViewerMode.None },
            photoViewer.Supported.ToArray());
        Assert.AreEqual(PhotoViewerMode.Page, photoViewer.Default);
    }

    [TestMethod]
    public void GetManifest_SupportedPhotoViewers_CannotBeMutatedByDowncast()
    {
        var themeDir = CreateThemeDirectory("TestTheme");

        var supported = new LocalThemeProvider(themeDir).Manifest.PhotoViewer?.Supported;

        Assert.IsNotNull(supported);
        Assert.IsFalse(supported is List<PhotoViewerMode>);
        var mutableView = Assert.IsInstanceOfType<IList<PhotoViewerMode>>(supported);
        Assert.ThrowsExactly<NotSupportedException>(() => mutableView[0] = PhotoViewerMode.None);
    }

    [TestMethod]
    [DataRow(true, false, "photoViewers")]
    [DataRow(false, true, "defaultPhotoViewer")]
    public void CreateManifest_BaseThemeMissingViewerField_ThrowsClearError(
        bool omitPhotoViewers,
        bool omitDefaultPhotoViewer,
        string expectedField)
    {
        // Arrange
        var config = CreateThemeConfig("TestTheme");
        config.PhotoViewers = omitPhotoViewers ? null : config.PhotoViewers;
        config.DefaultPhotoViewer = omitDefaultPhotoViewer ? null : config.DefaultPhotoViewer;

        // Act
        var exception = Assert.ThrowsExactly<InvalidOperationException>(config.CreateManifest);

        // Assert
        StringAssert.Contains(exception.Message, expectedField, StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateManifest_EmptyPhotoViewers_ThrowsClearError()
    {
        var config = CreateThemeConfig("TestTheme");
        config.PhotoViewers = [];

        var exception = Assert.ThrowsExactly<InvalidOperationException>(config.CreateManifest);

        StringAssert.Contains(exception.Message, "photoViewers", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "non-empty", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateManifest_DuplicatePhotoViewer_ThrowsClearError()
    {
        var config = CreateThemeConfig("TestTheme");
        config.PhotoViewers = ["page", "PAGE"];

        var exception = Assert.ThrowsExactly<InvalidOperationException>(config.CreateManifest);

        StringAssert.Contains(exception.Message, "photoViewers", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "PAGE", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateManifest_UnknownPhotoViewer_ThrowsClearError()
    {
        var config = CreateThemeConfig("TestTheme");
        config.PhotoViewers = ["carousel"];

        var exception = Assert.ThrowsExactly<InvalidOperationException>(config.CreateManifest);

        StringAssert.Contains(exception.Message, "photoViewers", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "carousel", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateManifest_UnsupportedDefaultPhotoViewer_ThrowsClearError()
    {
        var config = CreateThemeConfig("TestTheme");
        config.PhotoViewers = ["page"];
        config.DefaultPhotoViewer = "lightbox";

        var exception = Assert.ThrowsExactly<InvalidOperationException>(config.CreateManifest);

        StringAssert.Contains(exception.Message, "defaultPhotoViewer", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "lightbox", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateManifest_ExtensionDeclaringPhotoViewerFields_ThrowsClearError()
    {
        var config = CreateThemeConfig("TestTheme");
        config.TargetTheme = "Lumina";

        var exception = Assert.ThrowsExactly<InvalidOperationException>(config.CreateManifest);

        StringAssert.Contains(exception.Message, "photoViewers", StringComparison.Ordinal);
    }

    [TestMethod]
    public void GetFile_ExistingFile_ReturnsStream()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");
        File.WriteAllText(Path.Combine(themeDir, "Layout.revela"), "<html></html>");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        using var stream = adapter.GetFile("Layout.revela");

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetFile_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        var stream = adapter.GetFile("does-not-exist.revela");

        // Assert
        Assert.IsNull(stream);
    }

    [TestMethod]
    public void GetAllFiles_ReturnsRelativePaths()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");
        var assetsDir = Path.Combine(themeDir, "Assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(themeDir, "Layout.revela"), "<html></html>");
        File.WriteAllText(Path.Combine(assetsDir, "main.css"), "body {}");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        var files = adapter.GetAllFiles().ToList();

        // Assert
        Assert.IsNotEmpty(files);
        // theme.json is excluded
        Assert.IsFalse(files.Any(f =>
            f.Equals("theme.json", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetAllFiles_ExcludesThemeJson()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");
        File.WriteAllText(Path.Combine(themeDir, "Layout.revela"), "<html></html>");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        var files = adapter.GetAllFiles().ToList();

        // Assert
        Assert.IsFalse(files.Any(f =>
            f.Equals("theme.json", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetSiteTemplate_WithConfigFile_ReturnsStream()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");
        var configDir = Path.Combine(themeDir, "Configuration");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "site.json"), "{}");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        using var stream = adapter.GetSiteTemplate();

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetSiteTemplate_WithoutConfigFile_ReturnsNull()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        var stream = adapter.GetSiteTemplate();

        // Assert
        Assert.IsNull(stream);
    }

    [TestMethod]
    public void ThemeDirectory_ReturnsPath()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");

        // Act
        var adapter = new LocalThemeProvider(themeDir);

        // Assert
        Assert.AreEqual(themeDir, adapter.ThemeDirectory);
    }

    [TestMethod]
    public async Task ExtractToAsync_CopiesFiles()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");
        await File.WriteAllTextAsync(Path.Combine(themeDir, "Layout.revela"), "<html></html>");
        var outputDir = Path.Combine(tempDirectory, "output");

        // Act
        var adapter = new LocalThemeProvider(themeDir);
        await adapter.ExtractToAsync(outputDir);

        // Assert
        var extractedFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
        Assert.IsNotEmpty(extractedFiles);
    }

    private string CreateThemeDirectory(
        string name,
        string? layout = null,
        Action<ThemeJsonConfig>? configure = null)
    {
        var themeDir = Path.Combine(tempDirectory, name);
        Directory.CreateDirectory(themeDir);

        var themeConfig = CreateThemeConfig(name);

        if (layout is not null)
        {
            themeConfig.Templates = new ThemeTemplatesConfig { Layout = layout };
        }

        configure?.Invoke(themeConfig);

        var json = JsonSerializer.Serialize(themeConfig, ThemeJsonConfig.JsonOptions);
        File.WriteAllText(Path.Combine(themeDir, "theme.json"), json);

        return themeDir;
    }

    private static ThemeJsonConfig CreateThemeConfig(string name) => new()
    {
        Name = name,
        Version = "1.0.0",
        Description = $"Test theme {name}",
        Author = "Test",
        PhotoViewers = ["page", "lightbox", "none"],
        DefaultPhotoViewer = "page"
    };
}

