using Spectara.Revela.Theme.Lumina.Statistics;

namespace Spectara.Revela.Core.Tests.Themes;

/// <summary>
/// Unit tests for <see cref="Sdk.Themes.EmbeddedThemeExtension"/> via <see cref="LuminaStatisticsExtension"/>
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class EmbeddedThemeExtensionTests
{
    private LuminaStatisticsExtension extension = null!;

    [TestInitialize]
    public void Setup()
    {
        extension = new LuminaStatisticsExtension();
    }

    [TestMethod]
    public void Metadata_ReturnsCorrectName()
    {
        Assert.AreEqual("Lumina Statistics", extension.Metadata.Name);
    }

    [TestMethod]
    public void TargetTheme_ReturnsLumina()
    {
        Assert.AreEqual("Lumina", extension.TargetTheme);
    }

    [TestMethod]
    public void PartialPrefix_ReturnsStatistics()
    {
        Assert.AreEqual("statistics", extension.PartialPrefix);
    }

    [TestMethod]
    public void GetFile_ManifestJson_ReturnsStream()
    {
        // Arrange & Act
        using var stream = extension.GetFile("manifest.json");

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetFile_NonExistent_ReturnsNull()
    {
        // Arrange & Act
        var stream = extension.GetFile("does-not-exist.revela");

        // Assert
        Assert.IsNull(stream);
    }

    [TestMethod]
    public void GetAllFiles_ReturnsFiles()
    {
        // Arrange & Act
        var files = extension.GetAllFiles().ToList();

        // Assert
        Assert.IsNotEmpty(files);
    }

    [TestMethod]
    public void GetAllFiles_ContainsBarChart()
    {
        // Arrange & Act
        var files = extension.GetAllFiles().ToList();

        // Assert - should contain the bar-chart partial
        Assert.IsTrue(files.Any(f =>
            f.Contains("bar-chart", StringComparison.OrdinalIgnoreCase)),
            $"Files: {string.Join(", ", files)}");
    }

    [TestMethod]
    public void GetAllFiles_ContainsCss()
    {
        // Arrange & Act
        var files = extension.GetAllFiles().ToList();

        // Assert
        Assert.IsTrue(files.Any(f =>
            f.EndsWith(".css", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetTemplateDataDefaults_ExistingTemplate_ReturnsDefaults()
    {
        // Arrange & Act
        var defaults = extension.GetTemplateDataDefaults("statistics/overview");

        // Assert
        Assert.IsNotEmpty(defaults);
        Assert.Contains("statistics", defaults.Keys);
    }

    [TestMethod]
    public void GetTemplateDataDefaults_NonExistentTemplate_ReturnsEmpty()
    {
        // Arrange & Act
        var defaults = extension.GetTemplateDataDefaults("non-existent");

        // Assert
        Assert.IsEmpty(defaults);
    }

    [TestMethod]
    public async Task ExtractToAsync_ExtractsFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"ETE_Test_{Guid.NewGuid():N}");

        try
        {
            // Act
            await extension.ExtractToAsync(tempDir);

            // Assert
            var extractedFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
            Assert.IsTrue(extractedFiles.Length > 0);
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
