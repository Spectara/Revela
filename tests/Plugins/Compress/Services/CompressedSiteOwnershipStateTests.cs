using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;

using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress.Services;

/// <summary>
/// The ownership record lives in <c>.revela/state/compress.json</c>, never in the published output.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class CompressedSiteOwnershipStateTests
{
    private static readonly string[] ExpectedOutputFiles = ["index.html", "index.html.br", "index.html.gz"];

    private TestProject project = null!;
    private string original = null!;

    [TestInitialize]
    public async Task Setup()
    {
        project = TestProject.Create();
        Directory.CreateDirectory(project.OutputPath);
        original = Path.Combine(project.OutputPath, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
    }

    [TestCleanup]
    public void Cleanup() => project.Dispose();

    [TestMethod]
    public async Task CompressDirectoryAsync_RecordsOwnershipInStateNotOutput()
    {
        await CompressAsync();

        Assert.IsTrue(File.Exists(project.OwnershipRecord()));
        var outputFiles = Directory.GetFiles(project.OutputPath, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(ExpectedOutputFiles, outputFiles);
        Assert.IsEmpty(Directory.GetFiles(project.StateDirectory(), "*.tmp"));
    }

    [TestMethod]
    public async Task CompressDirectoryAsync_LegacyRecordInOutput_CarriesItOverAndReplacesOwnedSidecars()
    {
        // A site compressed by an earlier version: the record sits in the output next to the sidecars.
        await CompressAsync();
        File.Move(project.OwnershipRecord(), project.LegacyOwnershipRecord());
        Directory.Delete(project.StateDirectory(), recursive: true);

        var stats = await CompressAsync();

        Assert.AreEqual(1, stats.TotalFiles, "Sidecars from the legacy record must count as owned.");
        Assert.IsFalse(File.Exists(project.LegacyOwnershipRecord()), "The legacy record must leave the output.");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(project.OwnershipRecord()))!;
        Assert.HasCount(2, record["files"]!.AsArray());
    }

    [TestMethod]
    public async Task OpenAsync_LegacyRecordWithoutSidecarChanges_MovesRecordUnchanged()
    {
        await CompressAsync();
        var bytes = await File.ReadAllBytesAsync(project.OwnershipRecord());
        File.Move(project.OwnershipRecord(), project.LegacyOwnershipRecord());

        using (await CompressedSiteOwnership.OpenAsync(project.OutputPath, project.StateDirectory()))
        {
        }

        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(project.OwnershipRecord()));
        Assert.IsFalse(File.Exists(project.LegacyOwnershipRecord()));
    }

    [TestMethod]
    public async Task OpenAsync_LegacyAndStateRecordBothExist_KeepsStateRecordAndRemovesLegacy()
    {
        await CompressAsync();
        var current = await File.ReadAllBytesAsync(project.OwnershipRecord());
        await File.WriteAllTextAsync(project.LegacyOwnershipRecord(), /*lang=json,strict*/ """{ "stale": true }""");

        using (await CompressedSiteOwnership.OpenAsync(project.OutputPath, project.StateDirectory()))
        {
        }

        CollectionAssert.AreEqual(current, await File.ReadAllBytesAsync(project.OwnershipRecord()));
        Assert.IsFalse(File.Exists(project.LegacyOwnershipRecord()));
    }

    [TestMethod]
    public async Task OpenAsync_InvalidLegacyRecord_FailsAndLeavesEverythingInPlace()
    {
        await CompressAsync();
        File.Delete(project.OwnershipRecord());
        await File.WriteAllTextAsync(project.LegacyOwnershipRecord(), "{");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => CompressedSiteOwnership.OpenAsync(project.OutputPath, project.StateDirectory()));

        Assert.AreEqual("{", await File.ReadAllTextAsync(project.LegacyOwnershipRecord()));
        Assert.IsFalse(File.Exists(project.OwnershipRecord()));
        Assert.IsTrue(File.Exists(original + ".gz"));
    }

    private Task<CompressionStats> CompressAsync() =>
        new CompressionService(NullLogger<CompressionService>.Instance)
            .CompressDirectoryAsync(project.OutputPath, project.StateDirectory());
}
