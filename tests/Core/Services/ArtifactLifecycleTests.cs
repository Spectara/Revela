using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
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
            OperationResult.Fail("locked"));
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

    [TestMethod]
    public async Task InvalidateAllAsync_Cache_RemovesCacheArtifactsWithDependentsAndKeepsOutput()
    {
        var calls = new List<ArtifactId>();
        var graph = CoreGraph(calls);
        var pluginData = new FakeInvalidator(new ArtifactId("stats/data"), [CoreArtifacts.Manifest], calls, kind: ArtifactKind.Cache);
        var lifecycle = new ArtifactLifecycle([.. graph, pluginData]);

        var result = await lifecycle.InvalidateAllAsync([ArtifactKind.Cache]);

        Assert.IsTrue(result.Success, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { pluginData.Artifact, CoreArtifacts.Manifest }, calls);
    }

    [TestMethod]
    public async Task InvalidateAllAsync_Output_RemovesOutputArtifactsLeafFirstAndKeepsCache()
    {
        var calls = new List<ArtifactId>();
        var sidecars = new FakeInvalidator(new ArtifactId("zip/sidecars"), [CoreArtifacts.RenderedSite], calls);
        var lifecycle = new ArtifactLifecycle([.. CoreGraph(calls), sidecars]);

        var result = await lifecycle.InvalidateAllAsync([ArtifactKind.Output]);

        Assert.IsTrue(result.Success, result.ErrorMessage);
        CollectionAssert.AreEquivalent(
            new[] { sidecars.Artifact, CoreArtifacts.RenderedSite, CoreArtifacts.ProcessedImages },
            calls);
        Assert.IsLessThan(calls.IndexOf(CoreArtifacts.RenderedSite), calls.IndexOf(sidecars.Artifact), "Dependents go first.");
    }

    [TestMethod]
    public async Task InvalidateAllAsync_CacheAndOutput_KeepsDurableArtifacts()
    {
        var calls = new List<ArtifactId>();
        var captions = new FakeInvalidator(new ArtifactId("ai/captions"), [], calls, kind: ArtifactKind.Durable);
        var keywords = new FakeInvalidator(new ArtifactId("ai/keywords"), [captions.Artifact], calls, kind: ArtifactKind.Durable);
        var lifecycle = new ArtifactLifecycle([.. CoreGraph(calls), captions, keywords]);

        var result = await lifecycle.InvalidateAllAsync([ArtifactKind.Cache, ArtifactKind.Output]);

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.HasCount(3, calls);
        Assert.DoesNotContain(captions.Artifact, calls);
        Assert.DoesNotContain(keywords.Artifact, calls);
    }

    [TestMethod]
    public async Task InvalidateAllAsync_Durable_IsRejected()
    {
        var lifecycle = new ArtifactLifecycle(CoreGraph([]));

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await lifecycle.InvalidateAllAsync([ArtifactKind.Durable]));
    }

    [TestMethod]
    public async Task InvalidateAsync_DurableArtifact_RemovesItAndItsDependents()
    {
        var calls = new List<ArtifactId>();
        var captions = new FakeInvalidator(new ArtifactId("ai/captions"), [], calls, kind: ArtifactKind.Durable);
        var keywords = new FakeInvalidator(new ArtifactId("ai/keywords"), [captions.Artifact], calls, kind: ArtifactKind.Durable);
        var lifecycle = new ArtifactLifecycle([.. CoreGraph(calls), captions, keywords]);

        var result = await lifecycle.InvalidateAsync(captions.Artifact);

        Assert.IsTrue(result.Success, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { keywords.Artifact, captions.Artifact }, calls);
    }

    [TestMethod]
    public async Task InvalidateAsync_UnknownArtifact_ReturnsFailure()
    {
        var lifecycle = new ArtifactLifecycle(CoreGraph([]));

        var result = await lifecycle.InvalidateAsync(new ArtifactId("missing/data"));

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "Unknown artifact", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task InvalidateAllAsync_DurableDependsOnCacheArtifact_ReturnsFailure()
    {
        // Otherwise every rescan (replacing the manifest) would delete the durable data.
        var calls = new List<ArtifactId>();
        var captions = new FakeInvalidator(new ArtifactId("ai/captions"), [CoreArtifacts.Manifest], calls, kind: ArtifactKind.Durable);
        var lifecycle = new ArtifactLifecycle([.. CoreGraph(calls), captions]);

        var result = await lifecycle.InvalidateAllAsync([ArtifactKind.Cache]);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "durable", StringComparison.OrdinalIgnoreCase);
        Assert.IsEmpty(calls);
    }

    private static IArtifactInvalidator[] CoreGraph(List<ArtifactId> calls) =>
    [
        new FakeInvalidator(CoreArtifacts.Manifest, [], calls, kind: ArtifactKind.Cache),
        new FakeInvalidator(CoreArtifacts.RenderedSite, [], calls),
        new FakeInvalidator(CoreArtifacts.ProcessedImages, [], calls),
    ];

    private sealed class FakeInvalidator(
        ArtifactId artifact,
        IReadOnlyCollection<ArtifactId> dependsOn,
        List<ArtifactId> calls,
        OperationResult? result = null,
        ArtifactKind kind = ArtifactKind.Output) : IArtifactInvalidator
    {
        public ArtifactId Artifact { get; } = artifact;

        public ArtifactKind Kind { get; } = kind;

        public IReadOnlyCollection<ArtifactId> DependsOn { get; } = dependsOn;

        public ValueTask<OperationResult> InvalidateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            calls.Add(Artifact);
            return new ValueTask<OperationResult>(result ?? OperationResult.Ok());
        }
    }

    private sealed class ThrowingInvalidator : IArtifactInvalidator
    {
        public ArtifactId Artifact { get; } = new("example/throwing");

        public ArtifactKind Kind => ArtifactKind.Output;

        public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.RenderedSite];

        public ValueTask<OperationResult> InvalidateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("enumeration failed");
        }
    }
}
