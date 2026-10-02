using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress.Services;

/// <summary>
/// The ownership record lives in <c>.revela/compress/ownership.json</c>, never in the published output,
/// and is an output artifact: it goes together with the sidecars.
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
            .CompressDirectoryAsync(project.OutputPath, project.OwnerDirectory());

        Assert.IsTrue(File.Exists(project.OwnershipRecord()));
        var outputFiles = Directory.GetFiles(project.OutputPath, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(ExpectedOutputFiles, outputFiles);
        Assert.IsEmpty(Directory.GetFiles(project.OwnerDirectory(), "*.tmp"));
    }

    [TestMethod]
    public void Invalidator_IsAnOutputArtifactOfTheRenderedSite()
    {
        using var project = TestProject.Create();
        var invalidator = new CompressedSiteInvalidator(Substitute.For<IPathResolver>(), project.Environment());

        Assert.AreEqual(ArtifactKind.Output, invalidator.Kind);
        Assert.AreEqual("compress", invalidator.Artifact.Owner);
        CollectionAssert.AreEqual(new[] { CoreArtifacts.RenderedSite }, invalidator.DependsOn.ToArray());
    }

    [TestMethod]
    public async Task InvalidateAsync_OutputAlreadyGone_RemovesTheRecord()
    {
        // What `clean output` leaves when the output was deleted by hand: a record of nothing.
        using var project = TestProject.Create();
        Directory.CreateDirectory(project.OutputPath);
        await File.WriteAllTextAsync(Path.Combine(project.OutputPath, "index.html"), new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance)
            .CompressDirectoryAsync(project.OutputPath, project.OwnerDirectory());
        Directory.Delete(project.OutputPath, recursive: true);
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(project.OutputPath);

        var result = await new CompressedSiteInvalidator(pathResolver, project.Environment()).InvalidateAsync();

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.IsFalse(File.Exists(project.OwnershipRecord()));
    }
}
