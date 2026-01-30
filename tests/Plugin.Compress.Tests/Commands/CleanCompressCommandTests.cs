using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Plugin.Compress.Commands;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Plugin.Compress.Tests.Commands;

[TestClass]
[TestCategory("Unit")]
public sealed class CleanCompressCommandTests
{
    private string testDirectory = null!;
    private CleanCompressCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "revela-compress-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDirectory);

        var logger = NullLogger<CleanCompressCommand>.Instance;
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(testDirectory);

        command = new CleanCompressCommand(logger, pathResolver);
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
    public void Create_ReturnsCommand()
    {
        // Act
        var cmd = command.Create();

        // Assert
        Assert.AreEqual("compress", cmd.Name);
        Assert.IsNotNull(cmd.Description);
    }

    [TestMethod]
    public void Order_Is40()
    {
        // CleanCompressCommand should run after statistics (30) in clean menu
        var order = CleanCompressCommand.Order;
        Assert.AreEqual(40, order);
    }

    [TestMethod]
    public async Task Execute_DeletesGzipFiles()
    {
        // Arrange
        var htmlPath = Path.Combine(testDirectory, "index.html");
        var gzipPath = htmlPath + ".gz";
        await File.WriteAllTextAsync(htmlPath, "<html></html>");
        await File.WriteAllBytesAsync(gzipPath, [0x1f, 0x8b, 0x08]); // Gzip magic bytes

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
        await File.WriteAllTextAsync(htmlPath, "<html></html>");
        await File.WriteAllBytesAsync(brotliPath, [0x00, 0x00, 0x00]); // Dummy brotli

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
        await File.WriteAllBytesAsync(rootGz, [0x1f, 0x8b]);
        await File.WriteAllBytesAsync(subGz, [0x1f, 0x8b]);

        var cmd = command.Create();

        // Act
        var result = await cmd.Parse([]).InvokeAsync();

        // Assert
        Assert.AreEqual(0, result);
        Assert.IsFalse(File.Exists(rootGz));
        Assert.IsFalse(File.Exists(subGz));
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
