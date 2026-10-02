namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// How image encoding splits the machine: images processed in parallel (<see cref="Workers"/>)
/// × libvips threads per image (<see cref="ThreadsPerImage"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Without AVIF</b>, about one image per core with one or two threads each: JPEG and WebP
/// encode single-threaded, so a few images with many threads leave most cores idle. Measured on
/// 33 photos (JPEG q90): 4 cores −29 %, 32 threads −25 % wall time compared with half the cores
/// as images and up to 8 threads each. Fewer images than that get the free cores as threads.
/// </para>
/// <para>
/// <b>With AVIF</b>, the AV1 encoder dominates (over 90 % of the CPU time) and uses extra threads
/// to fill time the other threads wait. The previous split, half the cores as images with up to
/// 8 threads each, was as fast on 2 and 4 cores and up to 30 % faster on 32 threads for batches
/// that do not fill the machine, so it is kept. It also keeps AVIF files byte-identical:
/// libaom's output depends on its thread count.
/// </para>
/// <para>
/// Every parallel image holds its decoded original, a resized frame and the encoder state, about
/// 600–700 MB for a full-resolution AVIF of a 25–33 MP photo. Without a configured value the number
/// of parallel images is capped at one per <see cref="MemoryPerWorker"/> of the memory available
/// to Revela; without AVIF the cores left over go to the remaining images' threads.
/// </para>
/// </remarks>
internal readonly record struct ImageWorkerPlan(int Workers, int ThreadsPerImage)
{
    /// <summary>Memory budgeted per image processed in parallel.</summary>
    internal const long MemoryPerWorker = 1024L * 1024 * 1024;

    /// <summary>More libvips threads per image stop paying off (the AV1 encoder stops scaling).</summary>
    private const int MaxThreadsPerImage = 8;

    /// <summary>From this many logical processors, each image gets two threads (one per SMT sibling).</summary>
    private const int TwoThreadsFromProcessors = 8;

    /// <summary>
    /// Plans the split for a run.
    /// </summary>
    /// <param name="processorCount">Logical processors available to the process.</param>
    /// <param name="availableMemoryBytes">Memory available to the process; 0 or less when unknown.</param>
    /// <param name="configuredWorkers"><c>generate.images.maxDegreeOfParallelism</c>; wins over the CPU, memory and batch-size defaults.</param>
    /// <param name="imageCount">Images to encode in this run.</param>
    /// <param name="encodesAvif">Whether AVIF is one of the configured formats.</param>
    public static ImageWorkerPlan Create(int processorCount, long availableMemoryBytes, int? configuredWorkers, int imageCount, bool encodesAvif)
    {
        var processors = Math.Max(1, processorCount);
        var avifThreads = Math.Min(processors, MaxThreadsPerImage);

        if (configuredWorkers is { } configured)
        {
            var workers = Math.Max(1, configured);
            return new ImageWorkerPlan(workers, encodesAvif ? avifThreads : SplitCores(processors, workers));
        }

        var memoryWorkers = availableMemoryBytes > 0 ? Math.Max(1, availableMemoryBytes / MemoryPerWorker) : long.MaxValue;
        if (encodesAvif)
        {
            return new ImageWorkerPlan((int)Math.Min(Math.Max(1, processors / 2), memoryWorkers), avifThreads);
        }

        var threads = processors >= TwoThreadsFromProcessors ? 2 : 1;
        var cpuWorkers = (int)Math.Min(processors / threads, memoryWorkers);
        var plannedWorkers = Math.Clamp(imageCount, 1, cpuWorkers);
        return new ImageWorkerPlan(plannedWorkers, SplitCores(processors, plannedWorkers));
    }

    private static int SplitCores(int processors, int workers) => Math.Clamp(processors / workers, 1, MaxThreadsPerImage);
}
