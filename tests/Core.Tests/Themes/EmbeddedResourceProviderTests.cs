using Spectara.Revela.Sdk.Themes;
using Spectara.Revela.Theme.Lumina;

namespace Spectara.Revela.Core.Tests.Themes;

/// <summary>
/// Unit tests for <see cref="EmbeddedResourceProvider"/>
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class EmbeddedResourceProviderTests
{
    private EmbeddedResourceProvider provider = null!;

    [TestInitialize]
    public void Setup()
    {
        provider = new EmbeddedResourceProvider(typeof(LuminaThemePlugin).Assembly);
    }

    [TestMethod]
    public void AssemblyName_ReturnsCorrectName()
    {
        Assert.AreEqual("Spectara.Revela.Theme.Lumina", provider.AssemblyName);
    }

    [TestMethod]
    public void GetFile_ExistingFile_ReturnsStream()
    {
        // Arrange & Act
        using var stream = provider.GetFile("manifest.json");

        // Assert
        Assert.IsNotNull(stream);
        Assert.IsTrue(stream.Length > 0);
    }

    [TestMethod]
    public void GetFile_NonExistentFile_ReturnsNull()
    {
        // Arrange & Act
        var stream = provider.GetFile("does-not-exist.json");

        // Assert
        Assert.IsNull(stream);
    }

    [TestMethod]
    public void GetFile_ForwardSlashPath_ReturnsStream()
    {
        // Arrange & Act
        using var stream = provider.GetFile("Body/Gallery.revela");

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetFile_BackslashPath_ReturnsStream()
    {
        // Arrange & Act
        using var stream = provider.GetFile("Body\\Gallery.revela");

        // Assert
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public void GetAllFiles_ReturnsNonEmptyList()
    {
        // Arrange & Act
        var files = provider.GetAllFiles().ToList();

        // Assert
        Assert.IsNotEmpty(files);
    }

    [TestMethod]
    public void GetAllFiles_ContainsManifest()
    {
        // Arrange & Act
        var files = provider.GetAllFiles().ToList();

        // Assert
        Assert.IsTrue(files.Any(f =>
            f.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetAllFiles_ContainsTemplates()
    {
        // Arrange & Act
        var files = provider.GetAllFiles().ToList();

        // Assert
        Assert.IsTrue(files.Any(f =>
            f.EndsWith(".revela", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetAllFiles_ContainsAssets()
    {
        // Arrange & Act
        var files = provider.GetAllFiles().ToList();

        // Assert
        Assert.IsTrue(files.Any(f =>
            f.EndsWith(".css", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(files.Any(f =>
            f.EndsWith(".js", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetAllFiles_ExcludesCsFiles()
    {
        // Arrange & Act
        var files = provider.GetAllFiles().ToList();

        // Assert
        Assert.IsFalse(files.Any(f =>
            f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void LoadManifest_ValidManifest_ReturnsConfig()
    {
        // Arrange & Act
        var config = provider.LoadManifest<ThemeJsonConfig>();

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual("Lumina", config.Name);
    }

    [TestMethod]
    public async Task ExtractToAsync_ExtractsFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"ERP_Test_{Guid.NewGuid():N}");

        try
        {
            // Act
            await provider.ExtractToAsync(tempDir);

            // Assert
            var extractedFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
            Assert.IsTrue(extractedFiles.Length > 0);

            // Verify manifest.json was extracted
            Assert.IsTrue(extractedFiles.Any(f =>
                Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase)));
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
    public async Task ExtractToAsync_CancellationToken_Throws()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"ERP_Test_{Guid.NewGuid():N}");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        try
        {
            // Act & Assert
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                provider.ExtractToAsync(tempDir, cts.Token));
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
