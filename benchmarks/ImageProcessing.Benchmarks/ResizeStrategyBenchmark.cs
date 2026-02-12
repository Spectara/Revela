using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NetVips;

namespace ImageProcessing.Benchmarks;

/// <summary>
/// Benchmark comparing resize strategies for batch image processing.
///
/// Strategies tested:
/// - StarFromOriginal: Load original once, ThumbnailImage() for each size (current implementation)
/// - ThumbnailPerSize: Image.Thumbnail() for each size (re-reads from disk each time)
/// - ThumbnailThenResize: Thumbnail to largest, then ThumbnailImage() for smaller
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
public class ResizeStrategyBenchmark
{
    private string testImagePath = null!;
    private string outputDir = null!;

    // Realistic sizes for photography portfolio
    private static readonly int[] Sizes = [160, 320, 480, 640, 720, 960, 1280, 1440, 1920, 2560];

    [Params(1, 5)]
    public int ImageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Initialize NetVips
        NetVips.NetVips.Concurrency = Environment.ProcessorCount / 2;
        Cache.Max = 0;

        // Create test image (6000x4000 - typical modern camera)
        testImagePath = Path.Combine(Path.GetTempPath(), "benchmark-test-image.jpg");
        if (!File.Exists(testImagePath))
        {
            Console.WriteLine("Creating test image...");
            using var image = Image.Black(6000, 4000, bands: 3);
            // Add some variation to make it more realistic
            using var noise = Image.Gaussnoise(6000, 4000, mean: 128, sigma: 30);
            using var rgb = noise.Bandjoin(noise, noise);
            rgb.Jpegsave(testImagePath, q: 95);
        }

        outputDir = Path.Combine(Path.GetTempPath(), $"benchmark-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Clean output directory between iterations
        if (Directory.Exists(outputDir))
        {
            foreach (var dir in Directory.GetDirectories(outputDir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    /// <summary>
    /// Current implementation: Load original once, ThumbnailImage() for all sizes.
    /// Pro: Single disk read, correct alpha channel handling
    /// Con: Full image in memory, no shrink-on-load optimization
    /// </summary>
    [Benchmark(Baseline = true)]
    public void StarFromOriginal()
    {
        for (var i = 0; i < ImageCount; i++)
        {
            var imageDir = Path.Combine(outputDir, $"star_{i}");
            Directory.CreateDirectory(imageDir);

            using var original = Image.NewFromFile(testImagePath);
            var origWidth = original.Width;

            foreach (var size in Sizes.Where(s => s <= origWidth).OrderByDescending(s => s))
            {
                Image thumb;
                if (size >= origWidth)
                {
                    thumb = original;
                }
                else
                {
                    thumb = original.ThumbnailImage(size);
                }

                try
                {
                    var outPath = Path.Combine(imageDir, $"{size}.jpg");
                    thumb.Jpegsave(outPath, q: 90, keep: Enums.ForeignKeep.None);
                }
                finally
                {
                    if (thumb != original)
                    {
                        thumb.Dispose();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Recommended by libvips maintainer: Image.Thumbnail() per size.
    /// Pro: Shrink-on-load optimization, less memory
    /// Con: Multiple disk reads (but OS cache helps)
    /// </summary>
    [Benchmark]
    public void ThumbnailPerSize()
    {
        for (var i = 0; i < ImageCount; i++)
        {
            var imageDir = Path.Combine(outputDir, $"thumb_{i}");
            Directory.CreateDirectory(imageDir);

            // Get dimensions first (cheap header read)
            int origWidth;
            using (var header = Image.NewFromFile(testImagePath, access: Enums.Access.Sequential))
            {
                origWidth = header.Width;
            }

            foreach (var size in Sizes.Where(s => s <= origWidth).OrderByDescending(s => s))
            {
                // Thumbnail with shrink-on-load
                // height: 10000000 = effectively unconstrained, size applies to width/longest
                using var thumb = Image.Thumbnail(testImagePath, size, height: 10000000);

                var outPath = Path.Combine(imageDir, $"{size}.jpg");
                thumb.Jpegsave(outPath, q: 90, keep: Enums.ForeignKeep.None);
            }
        }
    }

    /// <summary>
    /// Hybrid: Thumbnail to largest needed size, then Resize() for smaller.
    /// Tries to get best of both worlds.
    /// </summary>
    [Benchmark]
    public void ThumbnailThenResize()
    {
        for (var i = 0; i < ImageCount; i++)
        {
            var imageDir = Path.Combine(outputDir, $"hybrid_{i}");
            Directory.CreateDirectory(imageDir);

            // Get dimensions
            int origWidth;
            using (var header = Image.NewFromFile(testImagePath, access: Enums.Access.Sequential))
            {
                origWidth = header.Width;
            }

            var applicableSizes = Sizes.Where(s => s <= origWidth).OrderByDescending(s => s).ToList();
            var largestSize = applicableSizes.FirstOrDefault();

            if (largestSize == 0)
            {
                continue;
            }

            // Load at largest needed size with shrink-on-load
            using var largest = Image.Thumbnail(testImagePath, largestSize, height: 10000000);
            var largestWidth = largest.Width;

            foreach (var size in applicableSizes)
            {
                Image thumb;
                if (size >= largestWidth)
                {
                    thumb = largest;
                }
                else
                {
                    thumb = largest.ThumbnailImage(size);
                }

                try
                {
                    var outPath = Path.Combine(imageDir, $"{size}.jpg");
                    thumb.Jpegsave(outPath, q: 90, keep: Enums.ForeignKeep.None);
                }
                finally
                {
                    if (thumb != largest)
                    {
                        thumb.Dispose();
                    }
                }
            }
        }
    }
}
