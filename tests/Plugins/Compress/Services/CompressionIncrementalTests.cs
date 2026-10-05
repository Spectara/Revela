using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;

using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress.Services;

/// <summary>
/// Incremental compression: a sidecar is reused while its source (SHA-256 + length) and its own
/// fingerprint still match the ownership record; the record is written in batches, not per file.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class CompressionIncrementalTests
{
    private const int ManyFiles = 1_000;
    private static readonly DateTime OldTimestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private TestProject project = null!;
    private string outputPath = null!;

    [TestInitialize]
    public void Setup()
    {
        project = TestProject.Create();
        outputPath = project.OutputPath;
        Directory.CreateDirectory(outputPath);
    }

    [TestCleanup]
    public void Cleanup() => project.Dispose();

    [TestMethod]
    public async Task CompressAndCleanAsync_ThousandFiles_RecordAccessStaysLinear()
    {
        // One scenario instead of three tests: creating 3,000 files dominates the run time.
        await WriteManyFilesAsync();

        using (var ownership = await CompressedSiteOwnership.OpenAsync(outputPath, project.OwnerDirectory()))
        {
            var stats = await CreateService().CompressDirectoryAsync(outputPath, ownership);

            Assert.AreEqual(ManyFiles, stats.CompressedCount);
            // 2,000 sidecars: a handful of checkpoints, not one record rewrite per sidecar.
            Assert.IsLessThanOrEqualTo(10, ownership.RecordWrites, $"Record writes: {ownership.RecordWrites}");
            Assert.IsLessThanOrEqualTo(10, ownership.RecordReads, $"Record reads: {ownership.RecordReads}");
        }

        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(project.OwnershipRecord()))!;
        Assert.HasCount(2 * ManyFiles, manifest["files"]!.AsArray());

        using (var ownership = await CompressedSiteOwnership.OpenAsync(outputPath, project.OwnerDirectory()))
        {
            var stats = await CreateService().CompressDirectoryAsync(outputPath, ownership);

            Assert.AreEqual(ManyFiles, stats.UnchangedCount);
            Assert.AreEqual(0, ownership.RecordWrites, "An unchanged site must not rewrite the record.");
            Assert.AreEqual(0, ownership.RecordReads);
        }

        using (var ownership = await CompressedSiteOwnership.OpenAsync(outputPath, project.OwnerDirectory()))
        {
            var cleaned = await ownership.CleanAsync();

            Assert.AreEqual(ManyFiles, cleaned.Gzip.FileCount);
            Assert.AreEqual(ManyFiles, cleaned.Brotli.FileCount);
            Assert.AreEqual(1, ownership.RecordWrites);
            Assert.AreEqual(1, ownership.RecordReads);
        }

        Assert.IsEmpty(Directory.GetFiles(outputPath, "*.gz", SearchOption.AllDirectories));
        Assert.IsEmpty(Directory.GetFiles(outputPath, "*.br", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_UnchangedSource_DoesNotRewriteSidecars()
    {
        var source = Path.Combine(outputPath, "index.html");
        await File.WriteAllTextAsync(source, new string('x', 1024));
        await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());
        File.SetLastWriteTimeUtc(source + ".gz", OldTimestamp);
        File.SetLastWriteTimeUtc(source + ".br", OldTimestamp);
        var gzip = await File.ReadAllBytesAsync(source + ".gz");
        var brotli = await File.ReadAllBytesAsync(source + ".br");
        var record = await File.ReadAllBytesAsync(project.OwnershipRecord());
        // A re-render writes identical bytes with a new timestamp.
        await File.WriteAllTextAsync(source, new string('x', 1024));
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddMinutes(1));

        var stats = await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());

        Assert.AreEqual(1, stats.TotalFiles);
        Assert.AreEqual(1, stats.UnchangedCount);
        Assert.AreEqual(0, stats.CompressedCount);
        Assert.AreEqual(1024L, stats.Gzip.OriginalSize);
        Assert.AreEqual(gzip.LongLength, stats.Gzip.CompressedSize);
        Assert.AreEqual(brotli.LongLength, stats.Brotli.CompressedSize);
        Assert.AreEqual(OldTimestamp, File.GetLastWriteTimeUtc(source + ".gz"));
        Assert.AreEqual(OldTimestamp, File.GetLastWriteTimeUtc(source + ".br"));
        CollectionAssert.AreEqual(gzip, await File.ReadAllBytesAsync(source + ".gz"));
        CollectionAssert.AreEqual(brotli, await File.ReadAllBytesAsync(source + ".br"));
        CollectionAssert.AreEqual(record, await File.ReadAllBytesAsync(project.OwnershipRecord()));
    }

    [TestMethod]
    [DataRow(1024)]
    [DataRow(2048)]
    public async Task CompressDirectoryAsync_ChangedSource_RecompressesSidecars(int newLength)
    {
        var source = Path.Combine(outputPath, "index.html");
        await File.WriteAllTextAsync(source, new string('x', 1024));
        await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());
        File.SetLastWriteTimeUtc(source + ".gz", OldTimestamp);
        File.SetLastWriteTimeUtc(source + ".br", OldTimestamp);
        var replacement = new string('y', newLength);
        await File.WriteAllTextAsync(source, replacement);
        // Same length and an older timestamp: only the content hash reveals the change.
        File.SetLastWriteTimeUtc(source, OldTimestamp);

        var stats = await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());

        Assert.AreEqual(1, stats.CompressedCount);
        Assert.AreEqual(0, stats.UnchangedCount);
        Assert.AreNotEqual(OldTimestamp, File.GetLastWriteTimeUtc(source + ".gz"));
        Assert.AreNotEqual(OldTimestamp, File.GetLastWriteTimeUtc(source + ".br"));
        Assert.AreEqual(replacement, await DecompressAsync(source + ".gz"));
        Assert.AreEqual(replacement, await DecompressAsync(source + ".br"));
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(project.OwnershipRecord()))!;
        var entries = manifest["files"]!.AsArray();
        Assert.HasCount(2, entries);
        Assert.IsTrue(entries.All(entry => entry!["sourceLength"]!.GetValue<long>() == newLength));
    }

    [TestMethod]
    [DataRow(".gz")]
    [DataRow(".br")]
    public async Task CompressDirectoryAsync_TamperedSidecarWithUnchangedSource_FailsAndPreservesBytes(string suffix)
    {
        var source = Path.Combine(outputPath, "index.html");
        await File.WriteAllTextAsync(source, new string('x', 1024));
        await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());
        var tampered = await File.ReadAllBytesAsync(source + suffix);
        tampered[0] ^= 0xff;
        await File.WriteAllBytesAsync(source + suffix, tampered);

        await Assert.ThrowsExactlyAsync<IOException>(() => CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory()));

        CollectionAssert.AreEqual(tampered, await File.ReadAllBytesAsync(source + suffix));
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(project.OwnershipRecord()))!;
        Assert.IsTrue(manifest["files"]!.AsArray().Any(entry => entry!["path"]!.GetValue<string>() == "index.html" + suffix));
        Assert.IsEmpty(Directory.GetFiles(outputPath, "*.tmp"));
    }

    [TestMethod]
    [DataRow(".gz")]
    [DataRow(".br")]
    public async Task CompressDirectoryAsync_DeletedSidecarWithUnchangedSource_RecreatesOnlyThatSidecar(string suffix)
    {
        var source = Path.Combine(outputPath, "index.html");
        var content = new string('x', 1024);
        await File.WriteAllTextAsync(source, content);
        await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());
        var other = source + (suffix == ".gz" ? ".br" : ".gz");
        File.SetLastWriteTimeUtc(other, OldTimestamp);
        File.Delete(source + suffix);

        var stats = await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());

        Assert.AreEqual(1, stats.CompressedCount);
        Assert.AreEqual(content, await DecompressAsync(source + suffix));
        Assert.AreEqual(OldTimestamp, File.GetLastWriteTimeUtc(other));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_RecordWithoutSourceFingerprint_RecompressesAndRecordsIt()
    {
        var source = Path.Combine(outputPath, "index.html");
        await File.WriteAllTextAsync(source, new string('x', 1024));
        await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());
        var manifestPath = project.OwnershipRecord();
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;
        foreach (var entry in manifest["files"]!.AsArray())
        {
            entry!.AsObject().Remove("sourceLength");
            entry.AsObject().Remove("sourceSha256");
        }
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString());

        var stats = await CreateService().CompressDirectoryAsync(outputPath, project.OwnerDirectory());

        Assert.AreEqual(1, stats.CompressedCount);
        var rewritten = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;
        Assert.IsTrue(rewritten["files"]!.AsArray().All(entry => entry!["sourceSha256"] is not null));
    }

    private async Task WriteManyFilesAsync()
    {
        for (var directory = 0; directory < 10; directory++)
        {
            var path = Path.Combine(outputPath, string.Create(CultureInfo.InvariantCulture, $"d{directory}"));
            Directory.CreateDirectory(path);
            for (var file = 0; file < ManyFiles / 10; file++)
            {
                var content = string.Create(CultureInfo.InvariantCulture, $"<p>{directory}-{file}</p>") + new string('x', 300);
                await File.WriteAllTextAsync(Path.Combine(path, string.Create(CultureInfo.InvariantCulture, $"p{file}.html")), content);
            }
        }
    }

    private static CompressionService CreateService() => new(NullLogger<CompressionService>.Instance);

    private static async Task<string> DecompressAsync(string path)
    {
        await using var file = File.OpenRead(path);
        await using var decompressor = path.EndsWith(".gz", StringComparison.Ordinal)
            ? (Stream)new GZipStream(file, CompressionMode.Decompress)
            : new BrotliStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressor);
        return await reader.ReadToEndAsync();
    }
}
