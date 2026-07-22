namespace Spectara.Revela.Features.Generate.Models.Results;

/// <summary>
/// Lock-free, allocation-light shared counters that the encode workers update
/// while processing images. A single reporter turns these into immutable
/// <see cref="ImageProgress"/> snapshots for the UI.
/// </summary>
/// <remarks>
/// <para>
/// Every mutator is O(1) and allocation-free — this is the hot path invoked
/// per variant (tens of thousands of times per run). Per-format counts use a
/// pre-populated map of single-element boxes so completions are a plain
/// <see cref="Interlocked.Increment(ref int)"/> with no dictionary mutation.
/// </para>
/// <para>
/// The format map is built once in the constructor and never mutated
/// afterwards, so concurrent reads during <see cref="Snapshot"/> are safe.
/// </para>
/// </remarks>
internal sealed class ImageProgressState
{
    private readonly IReadOnlyList<string> formats;
    private readonly Dictionary<string, int[]> doneByFormat;

    private int processed;
    private int skipped;
    private int workersBusy;

    public ImageProgressState(int total, IReadOnlyList<string> formats)
    {
        Total = total;
        this.formats = formats;
        doneByFormat = new Dictionary<string, int[]>(formats.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var format in formats)
        {
            doneByFormat[format] = new int[1];
        }
    }

    /// <summary>Total number of images to process.</summary>
    public int Total { get; }

    /// <summary>Images finished so far.</summary>
    public int Processed => Volatile.Read(ref processed);

    /// <summary>Marks a worker as having started encoding an image.</summary>
    public void WorkerStarted() => Interlocked.Increment(ref workersBusy);

    /// <summary>Marks a worker as having finished (or aborted) an image.</summary>
    public void WorkerFinished() => Interlocked.Decrement(ref workersBusy);

    /// <summary>Records a finished image and returns the new processed count.</summary>
    public int ImageCompleted() => Interlocked.Increment(ref processed);

    /// <summary>Records a completed variant for the given format (O(1), lock-free).</summary>
    public void VariantDone(string format)
    {
        if (doneByFormat.TryGetValue(format, out var box))
        {
            Interlocked.Increment(ref box[0]);
        }
    }

    /// <summary>Records a skipped variant (O(1), lock-free).</summary>
    public void VariantSkipped() => Interlocked.Increment(ref skipped);

    /// <summary>
    /// Builds an immutable snapshot of the current counters. The rolling rate
    /// and ETA are supplied by the reporter, which owns the (non-thread-safe)
    /// <see cref="ProgressRate"/>.
    /// </summary>
    public ImageProgress Snapshot(TimeSpan elapsed, double imagesPerMinute, TimeSpan? eta)
    {
        var done = new Dictionary<string, int>(formats.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var format in formats)
        {
            done[format] = Volatile.Read(ref doneByFormat[format][0]);
        }

        return new ImageProgress
        {
            Processed = Volatile.Read(ref processed),
            Total = Total,
            Skipped = Volatile.Read(ref skipped),
            DoneByFormat = done,
            WorkersBusy = Volatile.Read(ref workersBusy),
            ImagesPerMinute = imagesPerMinute,
            Elapsed = elapsed,
            Eta = eta
        };
    }
}
