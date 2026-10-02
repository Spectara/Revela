using System.IO.Compression;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;

using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class CompressionServiceTests
{
    private TestProject project = null!;
    private string testDirectory = null!;
    private CompressionService service = null!;

    [TestInitialize]
    public void Setup()
    {
        project = TestProject.Create();
        testDirectory = project.OutputPath;
        Directory.CreateDirectory(testDirectory);

        var logger = NullLogger<CompressionService>.Instance;
        service = new CompressionService(logger);
    }

    [TestCleanup]
    public void Cleanup() => project.Dispose();

    [TestMethod]
    public async Task CompressDirectoryAsync_CompressesHtmlFile()
    {
        // Arrange
        var htmlContent = "<html><body>" + new string('x', 1000) + "</body></html>";
        var htmlPath = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(htmlPath, htmlContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        await File.WriteAllTextAsync(smallPath, new string('x', 512));
        await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());
        await File.WriteAllTextAsync(smallPath, smallContent);

        // Act
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

        // Assert
        Assert.AreEqual(0, stats.TotalFiles);
        Assert.AreEqual(1, stats.SkippedCount);
        Assert.IsFalse(File.Exists(smallPath + ".gz"), "Small file should not be compressed");
        Assert.IsFalse(File.Exists(smallPath + ".br"), "Small file should not be compressed");
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_DirectoryLinkLeavesOutput_PreservesExternalSidecars()
    {
        using var external = TestProject.Create();
        var externalDirectory = external.OutputPath;
        var linkPath = Path.Combine(testDirectory, "linked");
        Directory.CreateDirectory(externalDirectory);
        var externalFile = Path.Combine(externalDirectory, "small.html");
        await File.WriteAllTextAsync(externalFile, "<html></html>");
        await File.WriteAllTextAsync(externalFile + ".gz", "external");
        await File.WriteAllTextAsync(externalFile + ".br", "external");
        DirectoryLinkTestHelper.Create(linkPath, externalDirectory);

        try
        {
            var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

            Assert.AreEqual(0, stats.TotalFiles);
            Assert.IsTrue(File.Exists(externalFile + ".gz"));
            Assert.IsTrue(File.Exists(externalFile + ".br"));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(linkPath);
            Directory.Delete(externalDirectory, recursive: true);
        }
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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        var stats = await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

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
        await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

        // Assert - verify brotli can be decompressed to original
        await using var brotliStream = new BrotliStream(
            File.OpenRead(htmlPath + ".br"),
            CompressionMode.Decompress);
        using var reader = new StreamReader(brotliStream);
        var decompressedContent = await reader.ReadToEndAsync();

        Assert.AreEqual(originalContent, decompressedContent);
    }

    [TestMethod]
    [DataRow(12, ".gz")]
    [DataRow(12, ".br")]
    [DataRow(512, ".gz")]
    [DataRow(512, ".br")]
    public async Task CompressDirectoryAsync_UnownedDestination_FailsWithoutChangingBytes(int sourceLength, string suffix)
    {
        var original = Path.Combine(testDirectory, "index.html");
        var destination = original + suffix;
        await File.WriteAllTextAsync(original, new string('x', sourceLength));
        await using (var stream = File.Create(destination))
        await using (var compressor = suffix == ".gz"
            ? (Stream)new GZipStream(stream, CompressionLevel.Optimal)
            : new BrotliStream(stream, CompressionLevel.Optimal))
        {
            await compressor.WriteAsync("unrelated download"u8.ToArray());
        }
        var bytes = await File.ReadAllBytesAsync(destination);

        await Assert.ThrowsExactlyAsync<IOException>(() => service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory()));

        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(destination));
        var manifestPath = project.OwnershipRecord();
        if (File.Exists(manifestPath))
        {
            var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;
            Assert.IsFalse(manifest["files"]!.AsArray().Any(entry => entry!["path"]!.GetValue<string>() == "index.html" + suffix));
        }
    }

    [TestMethod]
    [DataRow(12, ".gz")]
    [DataRow(12, ".br")]
    [DataRow(512, ".gz")]
    [DataRow(512, ".br")]
    public async Task CompressDirectoryAsync_ChangedOwnedBytes_FailsWithoutOverwritingReplacement(int sourceLength, string suffix)
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());
        var replacement = await File.ReadAllBytesAsync(original + suffix);
        replacement[0] ^= 0xff;
        await File.WriteAllBytesAsync(original + suffix, replacement);
        await File.WriteAllTextAsync(original, new string('y', sourceLength));

        var recreated = new CompressionService(NullLogger<CompressionService>.Instance);
        await Assert.ThrowsExactlyAsync<IOException>(() => recreated.CompressDirectoryAsync(testDirectory, project.OwnerDirectory()));

        CollectionAssert.AreEqual(replacement, await File.ReadAllBytesAsync(original + suffix));
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(project.OwnershipRecord()))!;
        Assert.IsTrue(manifest["files"]!.AsArray().Any(entry => entry!["path"]!.GetValue<string>() == "index.html" + suffix));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_RecreatedService_ReplacesOwnedFilesAndKeepsManifestOutOfInputs()
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());
        var replacement = new string('y', 768);
        await File.WriteAllTextAsync(original, replacement);

        var stats = await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory, project.OwnerDirectory());

        Assert.AreEqual(1, stats.TotalFiles);
        await using var gzip = new GZipStream(File.OpenRead(original + ".gz"), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        Assert.AreEqual(replacement, await reader.ReadToEndAsync());
        var manifestPath = project.OwnershipRecord();
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;
        Assert.AreEqual("Spectara.Revela.Plugins.Compress", manifest["owner"]!.GetValue<string>());
        Assert.AreEqual(1, manifest["version"]!.GetValue<int>());
        Assert.HasCount(2, manifest["files"]!.AsArray());
        Assert.IsFalse(File.Exists(manifestPath + ".gz"));
        Assert.IsFalse(File.Exists(manifestPath + ".br"));
    }

    [TestMethod]
    [DataRow(".gz", false)]
    [DataRow(".br", false)]
    [DataRow(".gz", true)]
    [DataRow(".br", true)]
    public async Task PublishAsync_CancelledStaging_PreservesExistingFileAndNeverPublishesPartial(string suffix, bool existing)
    {
        var original = Path.Combine(testDirectory, "index.html");
        var destination = original + suffix;
        if (existing)
        {
            await File.WriteAllTextAsync(original, new string('x', 512));
            await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());
        }
        var before = existing ? await File.ReadAllBytesAsync(destination) : [];
        var manifestPath = project.OwnershipRecord();
        var manifestBefore = existing ? await File.ReadAllBytesAsync(manifestPath) : [];
        using var cancellation = new CancellationTokenSource();
        using var ownership = await CompressedSiteOwnership.OpenAsync(testDirectory, project.OwnerDirectory());
        var staged = false;

        await Assert.ThrowsAsync<OperationCanceledException>(() => ownership.PublishAsync(destination, async (stream, token) =>
        {
            await using var compressor = suffix == ".gz"
                ? (Stream)new GZipStream(stream, CompressionLevel.SmallestSize, leaveOpen: true)
                : new BrotliStream(stream, CompressionLevel.SmallestSize, leaveOpen: true);
            await compressor.WriteAsync(new byte[4096], token);
            await compressor.FlushAsync(token);
            staged = true;
            await cancellation.CancelAsync();
        }, cancellation.Token));

        Assert.IsTrue(staged);
        Assert.AreEqual(existing, File.Exists(destination));
        Assert.IsEmpty(Directory.GetFiles(testDirectory, "*.tmp"));
        if (existing)
        {
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(destination));
            CollectionAssert.AreEqual(manifestBefore, await File.ReadAllBytesAsync(manifestPath));
        }
        else
        {
            Assert.IsFalse(File.Exists(manifestPath));
        }
    }

    [TestMethod]
    public async Task PublishAsync_StagingFails_PreservesOwnedFileAndRecord()
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());
        var before = await File.ReadAllBytesAsync(original + ".gz");
        var manifestPath = project.OwnershipRecord();
        var manifestBefore = await File.ReadAllBytesAsync(manifestPath);
        using var ownership = await CompressedSiteOwnership.OpenAsync(testDirectory, project.OwnerDirectory());

        await Assert.ThrowsExactlyAsync<IOException>(() => ownership.PublishAsync(original + ".gz", async (stream, token) =>
        {
            await stream.WriteAsync("partial"u8.ToArray(), token);
            throw new IOException("Synthetic staging failure");
        }));

        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(original + ".gz"));
        CollectionAssert.AreEqual(manifestBefore, await File.ReadAllBytesAsync(manifestPath));
        Assert.IsEmpty(Directory.GetFiles(testDirectory, "*.tmp"));
    }

    [TestMethod]
    [DataRow(".gz", false)]
    [DataRow(".br", false)]
    [DataRow(".gz", true)]
    [DataRow(".br", true)]
    public async Task PublishAsync_StagingCleanupFails_PreservesPrimaryExceptionAndOwnedFiles(string suffix, bool cancel)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows read-only file deletion is required for this failure injection.");
        }

        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());
        var gzipBefore = await File.ReadAllBytesAsync(original + ".gz");
        var brotliBefore = await File.ReadAllBytesAsync(original + ".br");
        var manifestPath = project.OwnershipRecord();
        var manifestBefore = await File.ReadAllBytesAsync(manifestPath);
        using var cancellation = new CancellationTokenSource();
        var primary = cancel
            ? (Exception)new OperationCanceledException(cancellation.Token)
            : new IOException("Synthetic staging failure");
        string? temporaryPath = null;

        try
        {
            using (var ownership = await CompressedSiteOwnership.OpenAsync(testDirectory, project.OwnerDirectory()))
            {
                var actual = await Assert.ThrowsAsync<Exception>(() => ownership.PublishAsync(original + suffix, async (stream, token) =>
                {
                    temporaryPath = ((FileStream)stream).Name;
                    await stream.WriteAsync("partial"u8.ToArray(), token);
                    await stream.DisposeAsync();
                    File.SetAttributes(temporaryPath, File.GetAttributes(temporaryPath) | FileAttributes.ReadOnly);
                    if (cancel)
                    {
                        await cancellation.CancelAsync();
                    }
                    throw primary;
                }, cancellation.Token));

                Assert.AreSame(primary, actual);
                if (cancel)
                {
                    Assert.AreEqual(cancellation.Token, ((OperationCanceledException)actual).CancellationToken);
                }
                CollectionAssert.AreEqual(gzipBefore, await File.ReadAllBytesAsync(original + ".gz"));
                CollectionAssert.AreEqual(brotliBefore, await File.ReadAllBytesAsync(original + ".br"));
                CollectionAssert.AreEqual(manifestBefore, await File.ReadAllBytesAsync(manifestPath));
                Assert.IsNotNull(temporaryPath);
                Assert.IsTrue(File.Exists(temporaryPath));
                var failures = actual.Data["Spectara.Revela.Plugins.Compress.CleanupFailures"] as string[];
                Assert.IsNotNull(failures);
                Assert.HasCount(1, failures);
                Assert.IsTrue(failures[0].Contains("Publish staging cleanup", StringComparison.Ordinal));
                Assert.IsTrue(failures[0].Contains(nameof(UnauthorizedAccessException), StringComparison.Ordinal));
                Assert.IsFalse(failures[0].Contains(testDirectory, StringComparison.Ordinal));

                var recoveredPath = Path.Combine(testDirectory, "recovered.html.gz");
                await ownership.PublishAsync(recoveredPath, async (stream, token) => await stream.WriteAsync("complete"u8.ToArray(), token));
                CollectionAssert.AreEqual("complete"u8.ToArray(), await File.ReadAllBytesAsync(recoveredPath));
            }

            using var recoveryCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var recreated = await CompressedSiteOwnership.OpenAsync(testDirectory, project.OwnerDirectory(), recoveryCancellation.Token);
            var cleaned = await recreated.CleanAsync(recoveryCancellation.Token);
            Assert.AreEqual(2, cleaned.Gzip.FileCount);
            Assert.AreEqual(1, cleaned.Brotli.FileCount);
            Assert.IsTrue(File.Exists(temporaryPath));
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                File.SetAttributes(temporaryPath, File.GetAttributes(temporaryPath) & ~FileAttributes.ReadOnly);
                File.Delete(temporaryPath);
            }
        }
    }

    [TestMethod]
    public async Task PublishAsync_ManifestLocked_RollsBackNewFileAndPreservesPreviousRecord()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows file sharing is required for this failure injection.");
        }
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await service.CompressDirectoryAsync(testDirectory, project.OwnerDirectory());
        var manifestPath = project.OwnershipRecord();
        var before = await File.ReadAllBytesAsync(manifestPath);
        using (var ownership = await CompressedSiteOwnership.OpenAsync(testDirectory, project.OwnerDirectory()))
        await using (var locked = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => ownership.PublishAsync(
                Path.Combine(testDirectory, "new.html.gz"), async (stream, token) => await stream.WriteAsync("complete"u8.ToArray(), token)));
            Assert.IsGreaterThan(0L, locked.Length);
        }

        Assert.IsFalse(File.Exists(Path.Combine(testDirectory, "new.html.gz")));
        Assert.IsTrue(File.Exists(original + ".gz"));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(manifestPath));
        Assert.IsEmpty(Directory.GetFiles(testDirectory, "*.tmp"));
        using var recreated = await CompressedSiteOwnership.OpenAsync(testDirectory, project.OwnerDirectory());
        var cleaned = await recreated.CleanAsync();
        Assert.AreEqual(1, cleaned.Gzip.FileCount);
        Assert.AreEqual(1, cleaned.Brotli.FileCount);
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
