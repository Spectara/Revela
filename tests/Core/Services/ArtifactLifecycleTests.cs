using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Tests.Core.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class ArtifactLifecycleTests
{
    [TestMethod]
    public async Task PrepareToReplaceAsync_TransitiveDependents_InvalidatesLeafFirst()
    {
        var calls = new List<ArtifactId>();
        var compressed = new FakeInvalidator(
            new ArtifactId("example/compressed"),
            [CoreArtifacts.RenderedSite],
            calls);
        var searchIndex = new FakeInvalidator(
            new ArtifactId("example/search-index"),
            [compressed.Artifact],
            calls);
        var lifecycle = new ArtifactLifecycle([compressed, searchIndex]);

        var result = await lifecycle.PrepareToReplaceAsync(CoreArtifacts.RenderedSite);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(
            new[] { searchIndex.Artifact, compressed.Artifact },
            calls);
    }

    [TestMethod]
    public async Task PrepareToReplaceAsync_InvalidatorFails_StopsBeforeRemainingInvalidators()
    {
        var calls = new List<ArtifactId>();
        var failing = new FakeInvalidator(
            new ArtifactId("example/a-failing"),
            [CoreArtifacts.RenderedSite],
            calls,
            ArtifactInvalidationResult.Fail("locked"));
        var remaining = new FakeInvalidator(
            new ArtifactId("example/z-remaining"),
            [CoreArtifacts.RenderedSite],
            calls);
        var lifecycle = new ArtifactLifecycle([failing, remaining]);

        var result = await lifecycle.PrepareToReplaceAsync(CoreArtifacts.RenderedSite);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "locked", StringComparison.Ordinal);
        CollectionAssert.AreEqual(new[] { failing.Artifact }, calls);
    }

    [TestMethod]
    public async Task PrepareToReplaceAsync_DependencyIsUnknown_ReturnsFailure()
    {
        var invalidator = new FakeInvalidator(
            new ArtifactId("example/derived"),
            [new ArtifactId("missing/source")],
            []);
        var lifecycle = new ArtifactLifecycle([invalidator]);

        var result = await lifecycle.PrepareToReplaceAsync(CoreArtifacts.RenderedSite);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "unknown artifact", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task PrepareToReplaceAsync_DuplicateOwner_ReturnsFailure()
    {
        var artifact = new ArtifactId("example/duplicate");
        var lifecycle = new ArtifactLifecycle([
            new FakeInvalidator(artifact, [CoreArtifacts.RenderedSite], []),
            new FakeInvalidator(artifact, [CoreArtifacts.RenderedSite], [])
        ]);

        var result = await lifecycle.PrepareToReplaceAsync(CoreArtifacts.RenderedSite);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "Multiple invalidators", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task PrepareToReplaceAsync_PluginClaimsCoreArtifact_ReturnsFailure()
    {
        var lifecycle = new ArtifactLifecycle([
            new FakeInvalidator(CoreArtifacts.Manifest, [CoreArtifacts.RenderedSite], [])
        ]);

        var result = await lifecycle.PrepareToReplaceAsync(CoreArtifacts.RenderedSite);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "owned by Revela Core", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task PrepareToReplaceAsync_InvalidatorThrows_ReturnsContextualFailure()
    {
        var invalidator = new ThrowingInvalidator();
        var lifecycle = new ArtifactLifecycle([invalidator]);

        var result = await lifecycle.PrepareToReplaceAsync(CoreArtifacts.RenderedSite);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, invalidator.Artifact.Value, StringComparison.Ordinal);
        StringAssert.Contains(result.ErrorMessage, "enumeration failed", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task PrepareToReplaceAsync_DependencyCycle_ReturnsFailure()
    {
        var first = new ArtifactId("example/first");
        var second = new ArtifactId("example/second");
        var lifecycle = new ArtifactLifecycle([
            new FakeInvalidator(first, [second], []),
            new FakeInvalidator(second, [first], [])
        ]);

        var result = await lifecycle.PrepareToReplaceAsync(first);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "cycle", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeInvalidator(
        ArtifactId artifact,
        IReadOnlyCollection<ArtifactId> dependsOn,
        List<ArtifactId> calls,
        ArtifactInvalidationResult? result = null) : IArtifactInvalidator
    {
        public ArtifactId Artifact { get; } = artifact;

        public IReadOnlyCollection<ArtifactId> DependsOn { get; } = dependsOn;

        public ValueTask<ArtifactInvalidationResult> InvalidateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            calls.Add(Artifact);
            return new ValueTask<ArtifactInvalidationResult>(result ?? ArtifactInvalidationResult.Ok());
        }
    }

    private sealed class ThrowingInvalidator : IArtifactInvalidator
    {
        public ArtifactId Artifact { get; } = new("example/throwing");

        public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.RenderedSite];

        public ValueTask<ArtifactInvalidationResult> InvalidateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("enumeration failed");
        }
    }
}
