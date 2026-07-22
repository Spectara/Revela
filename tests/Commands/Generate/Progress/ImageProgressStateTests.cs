using Spectara.Revela.Features.Generate.Models.Results;

namespace Spectara.Revela.Tests.Commands.Generate.Progress;

/// <summary>
/// Tests for <see cref="ImageProgressState"/> — lock-free counter aggregation
/// and snapshot construction from reported variant events.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ImageProgressStateTests
{
    private static readonly string[] Formats = ["avif", "webp", "jpg"];

    [TestMethod]
    public void Snapshot_AggregatesVariantEvents()
    {
        var state = new ImageProgressState(total: 10, Formats);

        state.VariantDone("avif");
        state.VariantDone("avif");
        state.VariantDone("webp");
        state.VariantSkipped();
        state.VariantSkipped();
        state.VariantSkipped();

        var snapshot = state.Snapshot(TimeSpan.FromSeconds(1), 60, TimeSpan.FromSeconds(9));

        Assert.AreEqual(2, snapshot.DoneByFormat["avif"]);
        Assert.AreEqual(1, snapshot.DoneByFormat["webp"]);
        Assert.AreEqual(0, snapshot.DoneByFormat["jpg"]);
        Assert.AreEqual(3, snapshot.Skipped);
    }

    [TestMethod]
    public void ImageCompleted_IncrementsProcessedAndReturnsNewCount()
    {
        var state = new ImageProgressState(total: 3, Formats);

        Assert.AreEqual(1, state.ImageCompleted());
        Assert.AreEqual(2, state.ImageCompleted());
        Assert.AreEqual(2, state.Processed);

        var snapshot = state.Snapshot(TimeSpan.Zero, 0, null);
        Assert.AreEqual(2, snapshot.Processed);
        Assert.AreEqual(3, snapshot.Total);
    }

    [TestMethod]
    public void WorkerStartedAndFinished_TrackBusyCount()
    {
        var state = new ImageProgressState(total: 5, Formats);

        state.WorkerStarted();
        state.WorkerStarted();
        state.WorkerFinished();

        Assert.AreEqual(1, state.Snapshot(TimeSpan.Zero, 0, null).WorkersBusy);
    }

    [TestMethod]
    public void Snapshot_IncludesAllConfiguredFormats()
    {
        var state = new ImageProgressState(total: 1, Formats);

        var snapshot = state.Snapshot(TimeSpan.Zero, 0, null);

        Assert.HasCount(3, snapshot.DoneByFormat);
        Assert.Contains("avif", snapshot.DoneByFormat.Keys);
        Assert.Contains("webp", snapshot.DoneByFormat.Keys);
        Assert.Contains("jpg", snapshot.DoneByFormat.Keys);
    }

    [TestMethod]
    public void VariantDone_WithUnknownFormat_IsIgnored()
    {
        var state = new ImageProgressState(total: 1, Formats);

        state.VariantDone("png");

        var snapshot = state.Snapshot(TimeSpan.Zero, 0, null);
        Assert.AreEqual(0, snapshot.DoneByFormat["avif"]);
        Assert.AreEqual(0, snapshot.DoneByFormat["webp"]);
        Assert.AreEqual(0, snapshot.DoneByFormat["jpg"]);
    }

    [TestMethod]
    public void Snapshot_CarriesRateAndEta()
    {
        var state = new ImageProgressState(total: 100, Formats);
        var eta = TimeSpan.FromMinutes(24);

        var snapshot = state.Snapshot(TimeSpan.FromMinutes(18), 132, eta);

        Assert.AreEqual(132, snapshot.ImagesPerMinute);
        Assert.AreEqual(TimeSpan.FromMinutes(18), snapshot.Elapsed);
        Assert.AreEqual(eta, snapshot.Eta);
    }

    [TestMethod]
    public void Counters_AreThreadSafeUnderConcurrentUpdates()
    {
        var state = new ImageProgressState(total: 4000, Formats);

        Parallel.For(0, 1000, _ =>
        {
            state.VariantDone("avif");
            state.VariantDone("webp");
            state.VariantSkipped();
            state.ImageCompleted();
        });

        var snapshot = state.Snapshot(TimeSpan.Zero, 0, null);
        Assert.AreEqual(1000, snapshot.DoneByFormat["avif"]);
        Assert.AreEqual(1000, snapshot.DoneByFormat["webp"]);
        Assert.AreEqual(1000, snapshot.Skipped);
        Assert.AreEqual(1000, snapshot.Processed);
    }
}
