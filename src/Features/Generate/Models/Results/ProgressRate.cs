namespace Spectara.Revela.Features.Generate.Models.Results;

/// <summary>
/// Computes a rolling-window throughput rate (images per minute) and an
/// honest ETA from timestamped completion counts.
/// </summary>
/// <remarks>
/// <para>
/// A naive average is misleading here: AVIF and JPG throughput differ by
/// roughly 17×, so early samples (mostly small/fast variants) would promise
/// a completion time the run can never keep. This helper only considers
/// samples inside a recent time <paramref name="window"/> and refuses to
/// estimate until it has a meaningful spread of data — callers show
/// "estimating…" until then.
/// </para>
/// <para>
/// Not thread-safe: it is fed from a single reporting path. The ETA it
/// returns is never negative.
/// </para>
/// </remarks>
internal sealed class ProgressRate(TimeSpan window)
{
    private static readonly TimeSpan MinSpread = TimeSpan.FromSeconds(2);

    private readonly Queue<(TimeSpan At, int Count)> samples = new();
    private (TimeSpan At, int Count)? last;

    /// <summary>
    /// Records a completion sample. <paramref name="at"/> is elapsed time since
    /// the run started; <paramref name="count"/> is the cumulative finished count.
    /// </summary>
    public void Record(TimeSpan at, int count)
    {
        samples.Enqueue((at, count));
        last = (at, count);

        var cutoff = at - window;
        while (samples.Count > 2 && samples.Peek().At < cutoff)
        {
            samples.Dequeue();
        }
    }

    /// <summary>
    /// Rolling throughput in items per minute, or <see langword="null"/> while
    /// there is not yet enough data to estimate honestly.
    /// </summary>
    public double? PerMinute
    {
        get
        {
            if (last is null || samples.Count < 2)
            {
                return null;
            }

            var (firstAt, firstCount) = samples.Peek();
            var (lastAt, lastCount) = last.Value;
            var span = lastAt - firstAt;
            if (span < MinSpread)
            {
                return null;
            }

            var delta = lastCount - firstCount;
            if (delta <= 0)
            {
                return null;
            }

            return delta / span.TotalMinutes;
        }
    }

    /// <summary>
    /// Estimates the time to finish <paramref name="remaining"/> items from the
    /// current rolling rate. Returns <see cref="TimeSpan.Zero"/> when nothing
    /// remains and <see langword="null"/> while still estimating. Never negative.
    /// </summary>
    public TimeSpan? Estimate(int remaining)
    {
        if (remaining <= 0)
        {
            return TimeSpan.Zero;
        }

        var rate = PerMinute;
        if (rate is null or <= 0)
        {
            return null;
        }

        return TimeSpan.FromMinutes(remaining / rate.Value);
    }
}
