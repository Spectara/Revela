using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Plugins.Compress.Commands;
using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress.Commands;

[TestClass]
[TestCategory("Unit")]
public sealed class CleanCompressCommandTests
{
    private TestProject project = null!;
    private string testDirectory = null!;
    private CleanCompressCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        project = TestProject.Create();
        testDirectory = project.OutputPath;
        Directory.CreateDirectory(testDirectory);

        var logger = NullLogger<CleanCompressCommand>.Instance;
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(testDirectory);

        command = new CleanCompressCommand(logger, pathResolver);
    }

    [TestCleanup]
    public void Cleanup() => project.Dispose();

    [TestMethod]
    public void Create_ReturnsCommand()
    {
        // Act
        var cmd = command.Create();

        // Assert
        Assert.AreEqual("compress", cmd.Name);
        Assert.IsNotNull(cmd.Description);
    }

    [TestMethod]
    public async Task Execute_DeletesGzipFiles()
    {
        // Arrange
        var htmlPath = Path.Combine(testDirectory, "index.html");
        var gzipPath = htmlPath + ".gz";
        await File.WriteAllTextAsync(htmlPath, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);

        var cmd = command.Create();

        // Act
        var result = await cmd.Parse([]).InvokeAsync();

        // Assert
        Assert.AreEqual(0, result);
        Assert.IsTrue(File.Exists(htmlPath), "Original file should remain");
        Assert.IsFalse(File.Exists(gzipPath), "Gzip file should be deleted");
    }

    [TestMethod]
    public async Task Execute_DeletesBrotliFiles()
    {
        // Arrange
        var htmlPath = Path.Combine(testDirectory, "index.html");
        var brotliPath = htmlPath + ".br";
        await File.WriteAllTextAsync(htmlPath, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);

        var cmd = command.Create();

        // Act
        var result = await cmd.Parse([]).InvokeAsync();

        // Assert
        Assert.AreEqual(0, result);
        Assert.IsTrue(File.Exists(htmlPath), "Original file should remain");
        Assert.IsFalse(File.Exists(brotliPath), "Brotli file should be deleted");
    }

    [TestMethod]
    public async Task Execute_HandlesSubdirectories()
    {
        // Arrange
        var subDir = Path.Combine(testDirectory, "pages");
        Directory.CreateDirectory(subDir);

        var rootGz = Path.Combine(testDirectory, "index.html.gz");
        var subGz = Path.Combine(subDir, "about.html.gz");
        await File.WriteAllTextAsync(Path.Combine(testDirectory, "index.html"), new string('x', 512));
        await File.WriteAllTextAsync(Path.Combine(subDir, "about.html"), new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);

        var cmd = command.Create();

        // Act
        var result = await cmd.Parse([]).InvokeAsync();

        // Assert
        Assert.AreEqual(0, result);
        Assert.IsFalse(File.Exists(rootGz));
        Assert.IsFalse(File.Exists(subGz));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_UnknownDownloads_PreservesBytes(bool pipeline)
    {
        var gzip = Path.Combine(testDirectory, "download.gz");
        var brotli = Path.Combine(testDirectory, "download.br");
        await File.WriteAllTextAsync(gzip, "unrelated gzip");
        await File.WriteAllTextAsync(brotli, "unrelated brotli");

        if (pipeline)
        {
            var result = await ((IPipelineStep)command).ExecuteAsync(CancellationToken.None);
            Assert.IsTrue(result.Success);
        }
        else
        {
            Assert.AreEqual(0, await command.Create().Parse([]).InvokeAsync());
        }

        Assert.AreEqual("unrelated gzip", await File.ReadAllTextAsync(gzip));
        Assert.AreEqual("unrelated brotli", await File.ReadAllTextAsync(brotli));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_ChangedOwnedFile_FailsAndPreservesReplacement(bool pipeline)
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        await File.WriteAllTextAsync(original + ".gz", "replacement");
        var manifestPath = Path.Combine(testDirectory, ".revela-compress.manifest");
        var manifest = await File.ReadAllBytesAsync(manifestPath);

        if (pipeline)
        {
            var result = await ((IPipelineStep)command).ExecuteAsync(CancellationToken.None);
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorMessage);
        }
        else
        {
            Assert.AreEqual(1, await command.Create().Parse([]).InvokeAsync());
        }

        Assert.AreEqual("replacement", await File.ReadAllTextAsync(original + ".gz"));
        Assert.IsTrue(File.Exists(original + ".br"));
        CollectionAssert.AreEqual(manifest, await File.ReadAllBytesAsync(manifestPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_LockedOwnedFile_FailsAndRetainsRecordForRetry(bool pipeline)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows file sharing is required for this failure injection.");
        }
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        var manifestPath = Path.Combine(testDirectory, ".revela-compress.manifest");
        var before = await File.ReadAllBytesAsync(manifestPath);
        await using (var locked = new FileStream(original + ".gz", FileMode.Open, FileAccess.Read, FileShare.None))
        {
            if (pipeline)
            {
                var result = await ((IPipelineStep)command).ExecuteAsync(CancellationToken.None);
                Assert.IsFalse(result.Success);
                Assert.IsNotNull(result.ErrorMessage);
            }
            else
            {
                Assert.AreEqual(1, await command.Create().Parse([]).InvokeAsync());
            }
            Assert.IsGreaterThan(0L, locked.Length);
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(manifestPath));
        }

        var resolver = Substitute.For<IPathResolver>();
        resolver.OutputPath.Returns(testDirectory);
        var recreated = new CleanCompressCommand(NullLogger<CleanCompressCommand>.Instance, resolver);
        var retry = await ((IPipelineStep)recreated).ExecuteAsync(CancellationToken.None);

        Assert.IsTrue(retry.Success);
        Assert.IsFalse(File.Exists(original + ".gz"));
        Assert.IsFalse(File.Exists(original + ".br"));
        Assert.IsTrue(File.Exists(original));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_OwnedOrphansAndUnrelatedDownloads_RemovesOnlyOwnedFiles(bool pipeline)
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        File.Delete(original);
        var download = Path.Combine(testDirectory, "download.gz");
        await File.WriteAllTextAsync(download, "unrelated");

        if (pipeline)
        {
            Assert.IsTrue((await ((IPipelineStep)command).ExecuteAsync(CancellationToken.None)).Success);
        }
        else
        {
            Assert.AreEqual(0, await command.Create().Parse([]).InvokeAsync());
        }

        Assert.IsFalse(File.Exists(original + ".gz"));
        Assert.IsFalse(File.Exists(original + ".br"));
        Assert.AreEqual("unrelated", await File.ReadAllTextAsync(download));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_Cancelled_PreservesOwnedFiles(bool pipeline)
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        if (pipeline)
        {
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await ((IPipelineStep)command).ExecuteAsync(cancellation.Token));
        }
        else
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => command.ExecuteAsync(cancellation.Token));
        }

        Assert.IsTrue(File.Exists(original + ".gz"));
        Assert.IsTrue(File.Exists(original + ".br"));
    }

    [TestMethod]
    public async Task Execute_NonExistentDirectory_ReturnsZero()
    {
        // Arrange - delete the test directory
        Directory.Delete(testDirectory, recursive: true);

        var cmd = command.Create();

        // Act
        var result = await cmd.Parse([]).InvokeAsync();

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task Execute_NoCompressedFiles_ReturnsZero()
    {
        // Arrange - only original files, no .gz or .br
        await File.WriteAllTextAsync(Path.Combine(testDirectory, "index.html"), "<html></html>");

        var cmd = command.Create();

        // Act
        var result = await cmd.Parse([]).InvokeAsync();

        // Assert
        Assert.AreEqual(0, result);
    }
}
