using Spectara.Revela.Theme.Lumina;

namespace Spectara.Revela.Core.Tests.Themes;

/// <summary>
/// Unit tests for <see cref="Sdk.Themes.EmbeddedThemePlugin"/> via <see cref="LuminaThemePlugin"/>
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class EmbeddedThemePluginTests
{
    private LuminaThemePlugin plugin = null!;

    [TestInitialize]
    public void Setup()
    {
        plugin = new LuminaThemePlugin();
    }

    [TestMethod]
    public void Metadata_ReturnsCorrectName()
    {
        Assert.AreEqual("Lumina", plugin.Metadata.Name);
    }

    [TestMethod]
    public void Metadata_ReturnsVersion()
    {
        Assert.IsFalse(string.IsNullOrEmpty(plugin.Metadata.Version));
    }

    [TestMethod]
    public void Metadata_HasTags()
    {
        Assert.IsNotEmpty(plugin.Metadata.Tags);
    }

    [TestMethod]
    public void GetManifest_ReturnsLayoutTemplate()
    {
        // Arrange & Act
        var manifest = plugin.GetManifest();

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(manifest.LayoutTemplate));
    }

    [TestMethod]
    public void GetManifest_ReturnsVariables()
    {
        // Arrange & Act
        var manifest = plugin.GetManifest();

        // Assert
        Assert.IsNotNull(manifest.Variables);
        Assert.Contains("credits", manifest.Variables.Keys);
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
    public void PluginMetadata_IPlugin_Metadata_IsThemeMetadata()
    {
        // Arrange & Act — access via IPlugin interface
        var pluginMetadata = ((Sdk.Abstractions.IPlugin)plugin).Metadata;

        // Assert
        Assert.AreEqual("Lumina", pluginMetadata.Name);
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
}
