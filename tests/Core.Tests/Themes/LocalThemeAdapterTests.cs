using System.Text.Json;

using Spectara.Revela.Core.Themes;
using Spectara.Revela.Sdk.Themes;

namespace Spectara.Revela.Core.Tests.Themes;

/// <summary>
/// Unit tests for <see cref="LocalThemeAdapter"/>
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class LocalThemeAdapterTests
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
        var adapter = new LocalThemeAdapter(themeDir);

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
            () => new LocalThemeAdapter(nonExistent));
    }

    [TestMethod]
    public void Constructor_MissingThemeJson_ThrowsFileNotFoundException()
    {
        // Arrange — directory exists but no theme.json
        var emptyDir = Path.Combine(tempDirectory, "empty-theme");
        Directory.CreateDirectory(emptyDir);

        // Act & Assert
        Assert.ThrowsExactly<FileNotFoundException>(
            () => new LocalThemeAdapter(emptyDir));
    }

    [TestMethod]
    public void GetManifest_ReturnsLayoutTemplate()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme", layout: "Custom.revela");

        // Act
        var adapter = new LocalThemeAdapter(themeDir);
        var manifest = adapter.GetManifest();

        // Assert
        Assert.AreEqual("Custom.revela", manifest.LayoutTemplate);
    }

    [TestMethod]
    public void GetManifest_DefaultLayout_UsesLayoutRevela()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");

        // Act
        var adapter = new LocalThemeAdapter(themeDir);
        var manifest = adapter.GetManifest();

        // Assert
        Assert.AreEqual("layout.revela", manifest.LayoutTemplate);
    }

    [TestMethod]
    public void GetManifest_WithVariables_ReturnsVariables()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme",
            variables: new Dictionary<string, string> { ["credits"] = "test credits" });

        // Act
        var adapter = new LocalThemeAdapter(themeDir);
        var manifest = adapter.GetManifest();

        // Assert
        Assert.Contains("credits", manifest.Variables.Keys);
        Assert.AreEqual("test credits", manifest.Variables["credits"]);
    }

    [TestMethod]
    public void GetFile_ExistingFile_ReturnsStream()
    {
        // Arrange
        var themeDir = CreateThemeDirectory("TestTheme");
        File.WriteAllText(Path.Combine(themeDir, "Layout.revela"), "<html></html>");

        // Act
        var adapter = new LocalThemeAdapter(themeDir);
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
        var adapter = new LocalThemeAdapter(themeDir);
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
        var adapter = new LocalThemeAdapter(themeDir);
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
        var adapter = new LocalThemeAdapter(themeDir);
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
        var adapter = new LocalThemeAdapter(themeDir);
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
        var adapter = new LocalThemeAdapter(themeDir);
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
        var adapter = new LocalThemeAdapter(themeDir);

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
        var adapter = new LocalThemeAdapter(themeDir);
        await adapter.ExtractToAsync(outputDir);

        // Assert
        var extractedFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
        Assert.IsNotEmpty(extractedFiles);
    }

    private string CreateThemeDirectory(
        string name,
        string? layout = null,
        Dictionary<string, string>? variables = null)
    {
        var themeDir = Path.Combine(tempDirectory, name);
        Directory.CreateDirectory(themeDir);

        var themeConfig = new ThemeJsonConfig
        {
            Name = name,
            Version = "1.0.0",
            Description = $"Test theme {name}",
            Author = "Test",
            Variables = variables
        };

        if (layout is not null)
        {
            themeConfig.Templates = new ThemeTemplatesConfig { Layout = layout };
        }

        var json = JsonSerializer.Serialize(themeConfig, ThemeJsonConfig.JsonOptions);
        File.WriteAllText(Path.Combine(themeDir, "theme.json"), json);

        return themeDir;
    }
}
