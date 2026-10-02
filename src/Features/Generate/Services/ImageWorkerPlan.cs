namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// How image encoding splits the machine: images processed in parallel (<see cref="Workers"/>)
/// × libvips threads per image (<see cref="ThreadsPerImage"/>).
/// </summary>
/// <remarks>
/// <para>
/// Encoders scale poorly inside one image: an AVIF encode with 8 libvips threads burns 1.7× the
/// CPU of a single thread for the same file. Running more images with fewer threads each is
/// therefore faster. Measured on 25 MP photos (AVIF + WebP + JPG): 2 cores 2×1 instead of 1×2
/// −21 %, 4 cores 4×1 instead of 2×4 −16 %; 32 threads JPEG-only 16×2 instead of 16×8 +62 %
/// images per minute.
/// </para>
/// <para>
/// Every parallel image holds its decoded original, a resized frame and the encoder state, about
/// 600–700 MB for a full-resolution AVIF of a 25–33 MP photo. Without a configured value the number
/// of parallel images is capped at one per <see cref="MemoryPerWorker"/> of the memory available
/// to Revela; cores left over go to the libvips threads of the remaining images.
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
    /// Plans the split for a machine.
    /// </summary>
    /// <param name="processorCount">Logical processors available to the process.</param>
    /// <param name="availableMemoryBytes">Memory available to the process; 0 or less when unknown.</param>
    /// <param name="configuredWorkers"><c>generate.images.maxDegreeOfParallelism</c>; wins over the CPU and memory defaults.</param>
    public static ImageWorkerPlan Create(int processorCount, long availableMemoryBytes, int? configuredWorkers)
    {
        var processors = Math.Max(1, processorCount);

        int workers;
        if (configuredWorkers is { } configured)
        {
            workers = Math.Max(1, configured);
        }
        else
        {
            var threads = processors >= TwoThreadsFromProcessors ? 2 : 1;
            workers = processors / threads;
            if (availableMemoryBytes > 0)
            {
                workers = (int)Math.Min(workers, Math.Max(1, availableMemoryBytes / MemoryPerWorker));
            }
        }

        return new ImageWorkerPlan(workers, Math.Clamp(processors / workers, 1, MaxThreadsPerImage));
    }
}
