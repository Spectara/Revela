using System.IO.Compression;

using Microsoft.Extensions.Logging.Abstractions;

using Spectara.Revela.Plugin.Compress.Services;

namespace Spectara.Revela.Plugin.Compress.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class CompressionServiceTests
{
    private string testDirectory = null!;
    private CompressionService service = null!;

    [TestInitialize]
    public void Setup()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "revela-compress-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDirectory);

        var logger = NullLogger<CompressionService>.Instance;
        service = new CompressionService(logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_CompressesHtmlFile()
    {
        // Arrange
        var htmlContent = "<html><body>" + new string('x', 1000) + "</body></html>";
        var htmlPath = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(htmlPath, htmlContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(1, stats.TotalFiles);
        Assert.IsTrue(File.Exists(htmlPath + ".gz"), "Gzip file should exist");
        Assert.IsTrue(File.Exists(htmlPath + ".br"), "Brotli file should exist");

        // Verify gzip is smaller than original
        var originalSize = new FileInfo(htmlPath).Length;
        var gzipSize = new FileInfo(htmlPath + ".gz").Length;
        Assert.IsLessThan(originalSize, gzipSize, "Gzip should be smaller than original");
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_CompressesCssFile()
    {
        // Arrange
        var cssContent = "body { " + string.Join(" ", Enumerable.Repeat("margin: 0;", 100)) + " }";
        var cssPath = Path.Combine(testDirectory, "style.css");
        await File.WriteAllTextAsync(cssPath, cssContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(1, stats.TotalFiles);
        Assert.IsTrue(File.Exists(cssPath + ".gz"));
        Assert.IsTrue(File.Exists(cssPath + ".br"));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_CompressesJsFile()
    {
        // Arrange
        var jsContent = "function test() { " + string.Join(" ", Enumerable.Repeat("console.log('x');", 100)) + " }";
        var jsPath = Path.Combine(testDirectory, "app.js");
        await File.WriteAllTextAsync(jsPath, jsContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(1, stats.TotalFiles);
        Assert.IsTrue(File.Exists(jsPath + ".gz"));
        Assert.IsTrue(File.Exists(jsPath + ".br"));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_CompressesJsonFile()
    {
        // Arrange
        var jsonContent = "{" + string.Join(",", Enumerable.Range(0, 100).Select(i => $"\"key{i}\":\"value{i}\"")) + "}";
        var jsonPath = Path.Combine(testDirectory, "data.json");
        await File.WriteAllTextAsync(jsonPath, jsonContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(1, stats.TotalFiles);
        Assert.IsTrue(File.Exists(jsonPath + ".gz"));
        Assert.IsTrue(File.Exists(jsonPath + ".br"));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_CompressesSvgFile()
    {
        // Arrange
        var svgContent = "<svg>" + string.Join("", Enumerable.Repeat("<circle r=\"10\"/>", 100)) + "</svg>";
        var svgPath = Path.Combine(testDirectory, "icon.svg");
        await File.WriteAllTextAsync(svgPath, svgContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(1, stats.TotalFiles);
        Assert.IsTrue(File.Exists(svgPath + ".gz"));
        Assert.IsTrue(File.Exists(svgPath + ".br"));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_CompressesXmlFile()
    {
        // Arrange
        var xmlContent = "<?xml version=\"1.0\"?><root>" +
            string.Join("", Enumerable.Repeat("<item>content</item>", 100)) + "</root>";
        var xmlPath = Path.Combine(testDirectory, "sitemap.xml");
        await File.WriteAllTextAsync(xmlPath, xmlContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(1, stats.TotalFiles);
        Assert.IsTrue(File.Exists(xmlPath + ".gz"));
        Assert.IsTrue(File.Exists(xmlPath + ".br"));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_SkipsSmallFiles()
    {
        // Arrange - file smaller than 256 bytes threshold
        var smallContent = "<html></html>";
        var smallPath = Path.Combine(testDirectory, "small.html");
        await File.WriteAllTextAsync(smallPath, smallContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(0, stats.TotalFiles);
        Assert.AreEqual(1, stats.SkippedCount);
        Assert.IsFalse(File.Exists(smallPath + ".gz"), "Small file should not be compressed");
        Assert.IsFalse(File.Exists(smallPath + ".br"), "Small file should not be compressed");
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_IgnoresImageFiles()
    {
        // Arrange - image files should not be compressed
        var jpgPath = Path.Combine(testDirectory, "photo.jpg");
        var pngPath = Path.Combine(testDirectory, "icon.png");
        await File.WriteAllBytesAsync(jpgPath, new byte[1000]);
        await File.WriteAllBytesAsync(pngPath, new byte[1000]);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(0, stats.TotalFiles);
        Assert.IsFalse(File.Exists(jpgPath + ".gz"));
        Assert.IsFalse(File.Exists(pngPath + ".gz"));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_HandlesSubdirectories()
    {
        // Arrange
        var subDir = Path.Combine(testDirectory, "pages", "about");
        Directory.CreateDirectory(subDir);

        var htmlContent = "<html><body>" + new string('x', 1000) + "</body></html>";
        await File.WriteAllTextAsync(Path.Combine(testDirectory, "index.html"), htmlContent);
        await File.WriteAllTextAsync(Path.Combine(subDir, "about.html"), htmlContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(2, stats.TotalFiles);
        Assert.IsTrue(File.Exists(Path.Combine(testDirectory, "index.html.gz")));
        Assert.IsTrue(File.Exists(Path.Combine(subDir, "about.html.gz")));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var htmlContent = "<html><body>" + new string('x', 1000) + "</body></html>";
        var cssContent = "body { " + string.Join(" ", Enumerable.Repeat("margin: 0;", 100)) + " }";
        await File.WriteAllTextAsync(Path.Combine(testDirectory, "index.html"), htmlContent);
        await File.WriteAllTextAsync(Path.Combine(testDirectory, "style.css"), cssContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(2, stats.TotalFiles);
        Assert.AreEqual(2, stats.Gzip.FileCount);
        Assert.AreEqual(2, stats.Brotli.FileCount);
        Assert.IsGreaterThan(0L, stats.Gzip.OriginalSize);
        Assert.IsGreaterThan(0L, stats.Gzip.CompressedSize);
        Assert.IsLessThan(stats.Gzip.OriginalSize, stats.Gzip.CompressedSize);
        Assert.IsGreaterThan(0.0, stats.Gzip.SavingsPercent);
        Assert.IsGreaterThan(0.0, stats.Brotli.SavingsPercent);
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_BrotliSmallerThanGzip()
    {
        // Arrange - repetitive content where Brotli excels
        var htmlContent = "<html><body>" + string.Join("", Enumerable.Repeat("<div>Hello World</div>", 500)) + "</body></html>";
        await File.WriteAllTextAsync(Path.Combine(testDirectory, "index.html"), htmlContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert - Brotli should typically achieve better compression
        Assert.IsLessThanOrEqualTo(
            stats.Gzip.CompressedSize,
            stats.Brotli.CompressedSize,
            $"Brotli ({stats.Brotli.CompressedSize}) should be <= Gzip ({stats.Gzip.CompressedSize})");
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_EmptyDirectory_ReturnsZeroStats()
    {
        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory);

        // Assert
        Assert.AreEqual(0, stats.TotalFiles);
        Assert.AreEqual(0, stats.SkippedCount);
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_GzipFileCanBeDecompressed()
    {
        // Arrange
        var originalContent = "<html><body>" + new string('x', 1000) + "</body></html>";
        var htmlPath = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(htmlPath, originalContent);

        // Act
        await service.CompressDirectoryAsync(testDirectory);

        // Assert - verify gzip can be decompressed to original
        await using var gzipStream = new GZipStream(
            File.OpenRead(htmlPath + ".gz"),
            CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        var decompressedContent = await reader.ReadToEndAsync();

        Assert.AreEqual(originalContent, decompressedContent);
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_BrotliFileCanBeDecompressed()
    {
        // Arrange
        var originalContent = "<html><body>" + new string('x', 1000) + "</body></html>";
        var htmlPath = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(htmlPath, originalContent);

        // Act
        await service.CompressDirectoryAsync(testDirectory);

        // Assert - verify brotli can be decompressed to original
        await using var brotliStream = new BrotliStream(
            File.OpenRead(htmlPath + ".br"),
            CompressionMode.Decompress);
        using var reader = new StreamReader(brotliStream);
        var decompressedContent = await reader.ReadToEndAsync();

        Assert.AreEqual(originalContent, decompressedContent);
    }

    [TestMethod]
    public void FormatSize_FormatsCorrectly()
    {
        Assert.AreEqual("100 B", CompressionService.FormatSize(100));
        Assert.AreEqual("1 KB", CompressionService.FormatSize(1024));
        Assert.AreEqual("1.5 KB", CompressionService.FormatSize(1536));
        Assert.AreEqual("1 MB", CompressionService.FormatSize(1024 * 1024));
        Assert.AreEqual("1.5 MB", CompressionService.FormatSize((long)(1.5 * 1024 * 1024)));
    }
}
