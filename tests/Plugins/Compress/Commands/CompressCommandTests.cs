using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

using Spectara.Revela.Plugins.Compress.Commands;
using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Tests.Plugins.Compress.Commands;

[TestClass]
[TestCategory("Unit")]
public sealed class CompressCommandTests : IDisposable
{
    private readonly string testDirectory = Path.Combine(
        Path.GetTempPath(),
        "revela-compress-command-tests",
        Guid.NewGuid().ToString());

    [TestMethod]
    public async Task ExecuteAsync_DependentInvalidationFails_PreservesOwnedArtifacts()
    {
        Directory.CreateDirectory(testDirectory);
        var sidecarPath = Path.Combine(testDirectory, "index.html.br");
        await File.WriteAllTextAsync(sidecarPath, "existing");
        var command = CreateCommand(new FailingArtifactLifecycle());

        var exitCode = await command.ExecuteAsync();

        Assert.AreEqual(1, exitCode);
        Assert.IsTrue(File.Exists(sidecarPath));
    }

    [TestMethod]
    public async Task ExecuteAsync_OrphanedSidecarExists_RemovesOrphanBeforeCompression()
    {
        Directory.CreateDirectory(testDirectory);
        var orphanedSidecarPath = Path.Combine(testDirectory, "removed.html.gz");
        var currentPath = Path.Combine(testDirectory, "index.html");
        await File.WriteAllTextAsync(orphanedSidecarPath, "orphaned");
        await File.WriteAllTextAsync(currentPath, new string('x', 512));
        var command = CreateCommand(new SuccessfulArtifactLifecycle());

        var exitCode = await command.ExecuteAsync();

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(File.Exists(orphanedSidecarPath));
        Assert.IsTrue(File.Exists(currentPath + ".gz"));
        Assert.IsTrue(File.Exists(currentPath + ".br"));
    }

    public void Dispose()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private CompressCommand CreateCommand(IArtifactLifecycle artifactLifecycle)
    {
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(testDirectory);
        var invalidator = new CompressedSiteInvalidator(pathResolver);
        return new CompressCommand(
            NullLogger<CompressCommand>.Instance,
            pathResolver,
            new CompressionService(NullLogger<CompressionService>.Instance),
            artifactLifecycle,
            invalidator);
    }

    private sealed class SuccessfulArtifactLifecycle : IArtifactLifecycle
    {
        public ValueTask<ArtifactInvalidationResult> PrepareToReplaceAsync(
            ArtifactId artifact,
            CancellationToken cancellationToken = default)
        {
            _ = artifact;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<ArtifactInvalidationResult>(ArtifactInvalidationResult.Ok());
        }
    }

    private sealed class FailingArtifactLifecycle : IArtifactLifecycle
    {
        public ValueTask<ArtifactInvalidationResult> PrepareToReplaceAsync(
            ArtifactId artifact,
            CancellationToken cancellationToken = default)
        {
            _ = artifact;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<ArtifactInvalidationResult>(
                ArtifactInvalidationResult.Fail("dependent cleanup failed"));
        }
    }
}
