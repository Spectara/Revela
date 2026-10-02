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

    [TestMethod]
    public async Task CompressDirectoryAsync_RecordsOwnershipInStateNotOutput()
    {
        using var project = TestProject.Create();
        Directory.CreateDirectory(project.OutputPath);
        await File.WriteAllTextAsync(Path.Combine(project.OutputPath, "index.html"), new string('x', 512));

        await new CompressionService(NullLogger<CompressionService>.Instance)
            .CompressDirectoryAsync(project.OutputPath, project.StateDirectory());

        Assert.IsTrue(File.Exists(project.OwnershipRecord()));
        var outputFiles = Directory.GetFiles(project.OutputPath, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(ExpectedOutputFiles, outputFiles);
        Assert.IsEmpty(Directory.GetFiles(project.StateDirectory(), "*.tmp"));
    }
}
