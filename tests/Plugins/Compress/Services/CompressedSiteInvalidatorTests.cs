using System.IO.Compression;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class CompressedSiteInvalidatorTests
{
    private TestProject project = null!;
    private string testDirectory = null!;
    private CompressedSiteInvalidator invalidator = null!;

    [TestInitialize]
    public void Setup()
    {
        project = TestProject.Create();
        testDirectory = project.OutputPath;
        Directory.CreateDirectory(testDirectory);

        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(testDirectory);
        invalidator = new CompressedSiteInvalidator(pathResolver);
    }

    [TestCleanup]
    public void Cleanup() => project.Dispose();

    [TestMethod]
    public async Task InvalidateAsync_UnrelatedCompressedDownloads_PreservesBytes()
    {
        using var project = TestProject.Create();
        Directory.CreateDirectory(project.OutputPath);
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(project.OutputPath);
        var service = new CompressedSiteInvalidator(pathResolver);

        foreach (var suffix in new[] { ".gz", ".br" })
        {
            var download = Path.Combine(project.OutputPath, "download" + suffix);
            await using var destination = File.Create(download);
            await using var compressor = suffix == ".gz"
                ? (Stream)new GZipStream(destination, CompressionLevel.Optimal)
                : new BrotliStream(destination, CompressionLevel.Optimal);
            await compressor.WriteAsync("unrelated download"u8.ToArray());
        }

        var gzipPath = Path.Combine(project.OutputPath, "download.gz");
        var brotliPath = Path.Combine(project.OutputPath, "download.br");
        var gzipBytes = await File.ReadAllBytesAsync(gzipPath);
        var brotliBytes = await File.ReadAllBytesAsync(brotliPath);

        var result = await service.InvalidateAsync();

        Assert.IsTrue(result.Success);
        Assert.IsTrue(File.Exists(gzipPath));
        Assert.IsTrue(File.Exists(brotliPath));
        CollectionAssert.AreEqual(gzipBytes, await File.ReadAllBytesAsync(gzipPath));
        CollectionAssert.AreEqual(brotliBytes, await File.ReadAllBytesAsync(brotliPath));
    }

    [TestMethod]
    public async Task InvalidateAsync_CompressedSidecarsExist_DeletesSidecarsOnly()
    {
        var nestedDirectory = Path.Combine(testDirectory, "assets");
        Directory.CreateDirectory(nestedDirectory);
        var original = Path.Combine(nestedDirectory, "main.css");
        var gzip = original + ".gz";
        var brotli = original + ".br";
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);

        var result = await invalidator.InvalidateAsync();

        Assert.IsTrue(result.Success);
        Assert.IsTrue(File.Exists(original));
        Assert.IsFalse(File.Exists(gzip));
        Assert.IsFalse(File.Exists(brotli));
    }

    [TestMethod]
    public async Task InvalidateAsync_RecreatedInvalidator_RemovesOwnedOrphansAndPreservesDownloads()
    {
        var original = Path.Combine(testDirectory, "removed.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        File.Delete(original);
        var cache = Path.Combine(project.RootPath, ProjectPaths.Cache);
        Directory.CreateDirectory(cache);
        await File.WriteAllTextAsync(Path.Combine(cache, "synthetic-cache"), "disposable");
        Directory.Delete(cache, recursive: true);
        var gzipDownload = Path.Combine(testDirectory, "download.gz");
        var brotliDownload = Path.Combine(testDirectory, "download.br");
        await File.WriteAllTextAsync(gzipDownload, "gzip download");
        await File.WriteAllTextAsync(brotliDownload, "brotli download");
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(testDirectory);
        var recreated = new CompressedSiteInvalidator(pathResolver);

        var result = await recreated.InvalidateAsync();

        Assert.IsTrue(result.Success);
        Assert.IsFalse(File.Exists(original + ".gz"));
        Assert.IsFalse(File.Exists(original + ".br"));
        Assert.AreEqual("gzip download", await File.ReadAllTextAsync(gzipDownload));
        Assert.AreEqual("brotli download", await File.ReadAllTextAsync(brotliDownload));
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(testDirectory, ".revela-compress.manifest")))!;
        Assert.IsEmpty(manifest["files"]!.AsArray());
    }

    [TestMethod]
    public async Task InvalidateAsync_RecordRemoved_PreservesFormerlyOwnedSidecars()
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        var gzip = await File.ReadAllBytesAsync(original + ".gz");
        var brotli = await File.ReadAllBytesAsync(original + ".br");
        File.Delete(Path.Combine(testDirectory, ".revela-compress.manifest"));

        var result = await invalidator.InvalidateAsync();

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(gzip, await File.ReadAllBytesAsync(original + ".gz"));
        CollectionAssert.AreEqual(brotli, await File.ReadAllBytesAsync(original + ".br"));
        Assert.IsFalse(File.Exists(Path.Combine(testDirectory, ".revela-compress.manifest")));
    }

    [TestMethod]
    [DataRow("malformed")]
    [DataRow("null")]
    [DataRow("owner")]
    [DataRow("version")]
    [DataRow("traversal")]
    [DataRow("absolute")]
    [DataRow("backslash")]
    [DataRow("empty-component")]
    [DataRow("trailing-dot")]
    [DataRow("suffix")]
    [DataRow("hash")]
    [DataRow("length")]
    [DataRow("duplicate-path")]
    [DataRow("null-entry")]
    [DataRow("null-files")]
    [DataRow("duplicate-property")]
    public async Task InvalidateAsync_InvalidManifest_FailsBeforeDeletingAnyEntry(string corruption)
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        var gzip = await File.ReadAllBytesAsync(original + ".gz");
        var brotli = await File.ReadAllBytesAsync(original + ".br");
        var manifestPath = Path.Combine(testDirectory, ".revela-compress.manifest");
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;
        var entries = manifest["files"]!.AsArray();
        switch (corruption)
        {
            case "owner":
                manifest["owner"] = "another producer";
                break;
            case "version":
                manifest["version"] = 2;
                break;
            case "traversal":
                entries[1]!["path"] = "../outside.br";
                break;
            case "absolute":
                entries[1]!["path"] = Path.Combine(project.RootPath, "outside.br");
                break;
            case "backslash":
                entries[1]!["path"] = "nested\\index.html.br";
                break;
            case "empty-component":
                entries[1]!["path"] = "nested//index.html.br";
                break;
            case "trailing-dot":
                entries[1]!["path"] = "nested./index.html.br";
                break;
            case "suffix":
                entries[1]!["path"] = "index.html";
                break;
            case "hash":
                entries[1]!["sha256"] = "invalid";
                break;
            case "length":
                entries[1]!["length"] = -1;
                break;
            case "duplicate-path":
                entries[1] = entries[0]!.DeepClone();
                break;
            case "null-entry":
                entries[1] = null;
                break;
            case "null-files":
                manifest["files"] = null;
                break;
            default:
                break;
        }
        var invalid = corruption switch
        {
            "malformed" => "{",
            "null" => "null",
            "duplicate-property" => manifest.ToJsonString().Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal),
            _ => manifest.ToJsonString()
        };
        await File.WriteAllTextAsync(manifestPath, invalid);

        var result = await invalidator.InvalidateAsync();

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        CollectionAssert.AreEqual(gzip, await File.ReadAllBytesAsync(original + ".gz"));
        CollectionAssert.AreEqual(brotli, await File.ReadAllBytesAsync(original + ".br"));
        Assert.AreEqual(invalid, await File.ReadAllTextAsync(manifestPath));
    }

    [TestMethod]
    public async Task InvalidateAsync_OwnedDescendantReplacedWithLink_FailsBeforeDeletingAnything()
    {
        using var external = TestProject.Create();
        Directory.CreateDirectory(external.OutputPath);
        var nested = Path.Combine(testDirectory, "nested");
        Directory.CreateDirectory(nested);
        var original = Path.Combine(nested, "index.html");
        var rootOriginal = Path.Combine(testDirectory, "root.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await File.WriteAllTextAsync(rootOriginal, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        Directory.Move(nested, Path.Combine(project.RootPath, "preserved"));
        var externalGzip = Path.Combine(external.OutputPath, "index.html.gz");
        await File.WriteAllTextAsync(externalGzip, "external download");
        DirectoryLinkTestHelper.Create(nested, external.OutputPath);

        try
        {
            var result = await invalidator.InvalidateAsync();

            Assert.IsFalse(result.Success);
            Assert.IsTrue(File.Exists(rootOriginal + ".gz"));
            Assert.IsTrue(File.Exists(rootOriginal + ".br"));
            Assert.AreEqual("external download", await File.ReadAllTextAsync(externalGzip));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(nested);
        }
    }

    [TestMethod]
    public async Task InvalidateAsync_ConfiguredRootIsLink_CleansOwnedFilesWithinChosenRoot()
    {
        var rootLink = Path.Combine(project.RootPath, "configured-root");
        DirectoryLinkTestHelper.Create(rootLink, testDirectory);
        try
        {
            var original = Path.Combine(rootLink, "index.html");
            await File.WriteAllTextAsync(original, new string('x', 512));
            await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(rootLink);
            var resolver = Substitute.For<IPathResolver>();
            resolver.OutputPath.Returns(rootLink);
            var linkedInvalidator = new CompressedSiteInvalidator(resolver);

            var result = await linkedInvalidator.InvalidateAsync();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(File.Exists(original));
            Assert.IsFalse(File.Exists(original + ".gz"));
            Assert.IsFalse(File.Exists(original + ".br"));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(rootLink);
        }
    }

    [TestMethod]
    public async Task InvalidateAsync_ChangedFingerprint_PreservesReplacementAndRecord()
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        var replacement = await File.ReadAllBytesAsync(original + ".gz");
        replacement[0] ^= 0xff;
        await File.WriteAllBytesAsync(original + ".gz", replacement);
        var manifestPath = Path.Combine(testDirectory, ".revela-compress.manifest");
        var before = await File.ReadAllBytesAsync(manifestPath);

        var result = await invalidator.InvalidateAsync();

        Assert.IsFalse(result.Success);
        CollectionAssert.AreEqual(replacement, await File.ReadAllBytesAsync(original + ".gz"));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(manifestPath));
    }

    [TestMethod]
    public async Task InvalidateAsync_Cancelled_PreservesOwnedArtifacts()
    {
        var original = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(testDirectory);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await invalidator.InvalidateAsync(cancellation.Token));

        Assert.IsTrue(File.Exists(original + ".gz"));
        Assert.IsTrue(File.Exists(original + ".br"));
    }

    [TestMethod]
    public async Task InvalidateAsync_DirectoryLinkLeavesArtifactRoot_PreservesExternalSidecars()
    {
        using var external = TestProject.Create();
        var externalDirectory = external.OutputPath;
        var linkPath = Path.Combine(testDirectory, "linked");
        Directory.CreateDirectory(externalDirectory);
        var externalSidecar = Path.Combine(externalDirectory, "external.css.gz");
        await File.WriteAllTextAsync(externalSidecar, "external");
        DirectoryLinkTestHelper.Create(linkPath, externalDirectory);

        try
        {
            var result = await invalidator.InvalidateAsync();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(File.Exists(externalSidecar));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(linkPath);
            Directory.Delete(externalDirectory, recursive: true);
        }
    }
}
