namespace Spectara.Revela.Features.Generate.Models.Results;

/// <summary>
/// Immutable snapshot of image-processing progress, consumed by the CLI to
/// render the live two-line display and the non-interactive heartbeat lines.
/// </summary>
/// <remarks>
/// Produced by <see cref="ImageProgressState.Snapshot"/> on a cheap cadence
/// (per finished image). It intentionally carries only aggregate counters —
/// there is no per-worker grid state — so building and reporting it is O(1)
/// on the encode threads and never takes a render lock.
/// </remarks>
internal sealed record ImageProgress
{
    private static readonly IReadOnlyDictionary<string, int> EmptyDoneByFormat =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Number of images fully processed so far.</summary>
    public int Processed { get; init; }

    /// <summary>Total number of images to process.</summary>
    public int Total { get; init; }

    /// <summary>Running count of skipped variants (already up-to-date on disk).</summary>
    public int Skipped { get; init; }

    /// <summary>Completed-variant counts per format (e.g. avif/webp/jpg).</summary>
    public IReadOnlyDictionary<string, int> DoneByFormat { get; init; } = EmptyDoneByFormat;

    /// <summary>Number of workers currently encoding an image.</summary>
    public int WorkersBusy { get; init; }

    /// <summary>Rolling throughput in finished images per minute (0 until estimable).</summary>
    public double ImagesPerMinute { get; init; }

    /// <summary>Wall-clock time elapsed since processing started.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Estimated time remaining, or <see langword="null"/> while still estimating.</summary>
    public TimeSpan? Eta { get; init; }
}

/// <summary>
/// Lifecycle state reported by the image processor for each variant slot.
/// </summary>
/// <remarks>
/// This is the processor → service callback contract used to count per-format
/// completions and skips. It is deliberately kept after the worker grid was
/// removed.
/// </remarks>
internal enum VariantState
{
    /// <summary>Encode is starting for this variant.</summary>
    Started,

    /// <summary>Variant was generated and written to disk.</summary>
    Done,

    /// <summary>Variant was skipped (already exists in cache).</summary>
    Skipped
}
