using Spectara.Revela.Features.Generate.Models.Results;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// Non-interactive progress reporter: writes throttled, plain-text heartbeat
/// lines to a <see cref="TextWriter"/> so redirected/CI/Docker runs are never
/// silent for the whole job.
/// </summary>
/// <remarks>
/// <para>
/// Emits on the first report and then at most once per <paramref name="interval"/>
/// (time based) or every <paramref name="everyImages"/> finished images,
/// whichever comes first. Output is pure text (no ANSI), keeping logs clean
/// and greppable and honouring <c>NO_COLOR</c>.
/// </para>
/// <para>
/// Reports arrive from encode worker threads, so emission is guarded by a lock;
/// this is cheap because it is heavily throttled — never the per-variant hot path.
/// </para>
/// </remarks>
internal sealed class HeartbeatProgressReporter(
    TextWriter writer,
    TimeProvider timeProvider,
    TimeSpan interval,
    int everyImages = 100) : IProgress<ImageProgress>
{
    private readonly Lock gate = new();
    private DateTimeOffset lastEmit = DateTimeOffset.MinValue;
    private int lastEmitProcessed = -1;
    private bool emitted;

    public void Report(ImageProgress value)
    {
        var now = timeProvider.GetUtcNow();

        lock (gate)
        {
            var dueByTime = !emitted || (now - lastEmit) >= interval;
            var dueByCount = value.Processed - lastEmitProcessed >= everyImages;
            if (!dueByTime && !dueByCount)
            {
                return;
            }

            emitted = true;
            lastEmit = now;
            lastEmitProcessed = value.Processed;
            writer.WriteLine(ImageProgressFormatter.Heartbeat(value));
        }
    }
}
