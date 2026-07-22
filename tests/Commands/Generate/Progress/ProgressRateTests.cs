using Spectara.Revela.Features.Generate.Models.Results;

namespace Spectara.Revela.Tests.Commands.Generate.Progress;

/// <summary>
/// Tests for <see cref="ProgressRate"/> — the rolling-window throughput/ETA helper.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ProgressRateTests
{
    private static ProgressRate Create() => new(TimeSpan.FromSeconds(60));

    [TestMethod]
    public void PerMinute_WithSingleSample_IsNull()
    {
        var rate = Create();
        rate.Record(TimeSpan.Zero, 0);

        Assert.IsNull(rate.PerMinute);
    }

    [TestMethod]
    public void PerMinute_WithinMinimumSpread_IsNull()
    {
        var rate = Create();
        rate.Record(TimeSpan.Zero, 0);
        rate.Record(TimeSpan.FromSeconds(1), 5);

        // Less than the 2s minimum spread → still estimating.
        Assert.IsNull(rate.PerMinute);
    }

    [TestMethod]
    public void PerMinute_AfterMeaningfulSpread_ComputesRate()
    {
        var rate = Create();
        rate.Record(TimeSpan.Zero, 0);
        rate.Record(TimeSpan.FromSeconds(3), 6);

        // 6 items over 3s = 120 items/minute.
        Assert.IsNotNull(rate.PerMinute);
        Assert.AreEqual(120.0, rate.PerMinute!.Value, 0.001);
    }

    [TestMethod]
    public void PerMinute_UsesOnlyRecentWindow()
    {
        var rate = new ProgressRate(TimeSpan.FromSeconds(10));
        rate.Record(TimeSpan.Zero, 0);
        rate.Record(TimeSpan.FromSeconds(5), 5);
        rate.Record(TimeSpan.FromSeconds(20), 25);

        // Oldest (0s,0) falls outside the 10s window and is dropped, so the rate
        // is measured from (5s,5)→(20s,25) = 20 items / 15s = 80/min, not the
        // naive 75/min the full history would give.
        Assert.IsNotNull(rate.PerMinute);
        Assert.AreEqual(80.0, rate.PerMinute!.Value, 0.001);
    }

    [TestMethod]
    public void Estimate_EarlyIsNull()
    {
        var rate = Create();
        rate.Record(TimeSpan.Zero, 0);

        Assert.IsNull(rate.Estimate(100));
    }

    [TestMethod]
    public void Estimate_StabilizesToPositiveValue()
    {
        var rate = Create();
        rate.Record(TimeSpan.Zero, 0);
        rate.Record(TimeSpan.FromSeconds(4), 8);

        // 8 items / 4s = 120/min. 60 remaining → 0.5 min = 30s.
        var eta = rate.Estimate(60);

        Assert.IsNotNull(eta);
        Assert.IsTrue(eta!.Value > TimeSpan.Zero, "ETA must be positive once estimable.");
        Assert.AreEqual(30.0, eta.Value.TotalSeconds, 0.5);
    }

    [TestMethod]
    public void Estimate_WithNothingRemaining_IsZero()
    {
        var rate = Create();
        rate.Record(TimeSpan.Zero, 0);
        rate.Record(TimeSpan.FromSeconds(4), 8);

        Assert.AreEqual(TimeSpan.Zero, rate.Estimate(0));
    }

    [TestMethod]
    public void Estimate_IsNeverNegative()
    {
        var rate = Create();
        rate.Record(TimeSpan.Zero, 0);
        rate.Record(TimeSpan.FromSeconds(2), 1);
        rate.Record(TimeSpan.FromSeconds(30), 200);

        var eta = rate.Estimate(5);

        Assert.IsNotNull(eta);
        Assert.IsTrue(eta!.Value >= TimeSpan.Zero, "ETA must never be negative.");
    }

    [TestMethod]
    public void PerMinute_WithNoForwardProgress_IsNull()
    {
        var rate = Create();
        rate.Record(TimeSpan.Zero, 5);
        rate.Record(TimeSpan.FromSeconds(5), 5);

        // No items completed over the window → cannot honestly estimate.
        Assert.IsNull(rate.PerMinute);
    }
}
