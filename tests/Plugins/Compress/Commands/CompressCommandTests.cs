using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Plugins.Compress.Commands;
using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress.Commands;

[TestClass]
[TestCategory("Unit")]
public sealed class CompressCommandTests : IDisposable
{
    private readonly TestProject project = TestProject.Create();
    private string TestDirectory => project.OutputPath;

    [TestMethod]
    public async Task ExecuteAsync_DependentInvalidationFails_PreservesOwnedArtifacts()
    {
        Directory.CreateDirectory(TestDirectory);
        var originalPath = Path.Combine(TestDirectory, "index.html");
        var sidecarPath = originalPath + ".br";
        await File.WriteAllTextAsync(originalPath, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(TestDirectory, project.StateDirectory());
        var command = CreateCommand(new FailingArtifactLifecycle());

        var exitCode = await command.ExecuteAsync();

        Assert.AreEqual(1, exitCode);
        Assert.IsTrue(File.Exists(sidecarPath));
    }

    [TestMethod]
    public async Task ExecuteAsync_OrphanedSidecarExists_RemovesOrphanBeforeCompression()
    {
        Directory.CreateDirectory(TestDirectory);
        var removedPath = Path.Combine(TestDirectory, "removed.html");
        var orphanedSidecarPath = removedPath + ".gz";
        var currentPath = Path.Combine(TestDirectory, "index.html");
        await File.WriteAllTextAsync(removedPath, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(TestDirectory, project.StateDirectory());
        File.Delete(removedPath);
        var gzipDownload = Path.Combine(TestDirectory, "download.gz");
        var brotliDownload = Path.Combine(TestDirectory, "download.br");
        await File.WriteAllTextAsync(gzipDownload, "gzip download");
        await File.WriteAllTextAsync(brotliDownload, "brotli download");
        await File.WriteAllTextAsync(currentPath, new string('x', 512));
        var command = CreateCommand(new SuccessfulArtifactLifecycle());

        var exitCode = await command.ExecuteAsync();

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(File.Exists(orphanedSidecarPath));
        Assert.IsTrue(File.Exists(currentPath + ".gz"));
        Assert.IsTrue(File.Exists(currentPath + ".br"));
        Assert.AreEqual("gzip download", await File.ReadAllTextAsync(gzipDownload));
        Assert.AreEqual("brotli download", await File.ReadAllTextAsync(brotliDownload));
    }

    [TestMethod]
    [DataRow(12, ".gz")]
    [DataRow(12, ".br")]
    [DataRow(512, ".gz")]
    [DataRow(512, ".br")]
    public async Task ExecuteAsync_UnownedSameNameDestination_ReturnsFailureAndPreservesBytes(int length, string suffix)
    {
        Directory.CreateDirectory(TestDirectory);
        var original = Path.Combine(TestDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', length));
        await File.WriteAllTextAsync(original + suffix, "unrelated download");
        var command = CreateCommand(new SuccessfulArtifactLifecycle());

        var result = await command.ExecuteAsync();

        Assert.AreEqual(1, result);
        Assert.AreEqual("unrelated download", await File.ReadAllTextAsync(original + suffix));
    }

    [TestMethod]
    public async Task ExecuteAsync_ResolverChangesDuringInvalidation_UsesCapturedRootAfterDependentsComplete()
    {
        using var other = TestProject.Create();
        Directory.CreateDirectory(TestDirectory);
        Directory.CreateDirectory(other.OutputPath);
        var original = Path.Combine(TestDirectory, "index.html");
        await File.WriteAllTextAsync(original, new string('x', 512));
        await new CompressionService(NullLogger<CompressionService>.Instance).CompressDirectoryAsync(TestDirectory, project.StateDirectory());
        var oldSidecar = await File.ReadAllBytesAsync(original + ".gz");
        var untouched = Path.Combine(other.OutputPath, "download.gz");
        await File.WriteAllTextAsync(untouched, "other root");
        var resolver = Substitute.For<IPathResolver>();
        resolver.OutputPath.Returns(TestDirectory);
        var called = false;
        var lifecycle = new SuccessfulArtifactLifecycle(() =>
        {
            CollectionAssert.AreEqual(oldSidecar, File.ReadAllBytes(original + ".gz"));
            resolver.OutputPath.Returns(other.OutputPath);
            called = true;
        });
        var command = new CompressCommand(
            NullLogger<CompressCommand>.Instance,
            resolver,
            project.Environment(),
            new CompressionService(NullLogger<CompressionService>.Instance),
            lifecycle,
            Substitute.For<IConsoleCapabilities>());

        var result = await command.ExecuteAsync();

        Assert.AreEqual(0, result);
        Assert.IsTrue(called);
        Assert.IsTrue(File.Exists(original + ".gz"));
        Assert.IsTrue(File.Exists(original + ".br"));
        Assert.AreEqual("other root", await File.ReadAllTextAsync(untouched));
        Assert.HasCount(1, Directory.GetFileSystemEntries(other.OutputPath), "Nothing may be written into the other root.");
    }

    public void Dispose() => project.Dispose();

    private CompressCommand CreateCommand(IArtifactLifecycle artifactLifecycle)
    {
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(TestDirectory);
        return new CompressCommand(
            NullLogger<CompressCommand>.Instance,
            pathResolver,
            project.Environment(),
            new CompressionService(NullLogger<CompressionService>.Instance),
            artifactLifecycle,
            Substitute.For<IConsoleCapabilities>());
    }

    private sealed class SuccessfulArtifactLifecycle(Action? beforeReplacement = null) : IArtifactLifecycle
    {
        public ValueTask<OperationResult> PrepareToReplaceAsync(
            ArtifactId artifact,
            CancellationToken cancellationToken = default)
        {
            _ = artifact;
            cancellationToken.ThrowIfCancellationRequested();
            beforeReplacement?.Invoke();
            return new ValueTask<OperationResult>(OperationResult.Ok());
        }
    }

    private sealed class FailingArtifactLifecycle : IArtifactLifecycle
    {
        public ValueTask<OperationResult> PrepareToReplaceAsync(
            ArtifactId artifact,
            CancellationToken cancellationToken = default)
        {
            _ = artifact;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<OperationResult>(
                OperationResult.Fail("dependent cleanup failed"));
        }
    }
}
