using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NetVips;

namespace ImageProcessing.Benchmarks;

/// <summary>
/// Benchmark comparing format-sequential vs all-formats-per-image processing.
///
/// Strategies tested:
/// - AllFormatsPerImage: Current approach - process all formats for each image before moving on
/// - FormatSequential: Process all images for JPG, then all for WebP, then all for AVIF
/// </summary>
[SimpleJob(RuntimeMoniker.Net90, iterationCount: 3, warmupCount: 1)]
[MemoryDiagnoser]
[RankColumn]
public class FormatSequentialBenchmark
{
    private string testImagePath = null!;
    private string outputDir = null!;

    private static readonly int[] Sizes = [320, 640, 1280, 1920];  // Reduced for faster benchmarks

    [Params(5, 10)]
    public int ImageCount { get; set; }

    [Params(false, true)]
    public bool IncludeAvif { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        NetVips.NetVips.Concurrency = Environment.ProcessorCount / 2;
        Cache.Max = 0;

        testImagePath = Path.Combine(Path.GetTempPath(), "benchmark-test-image.jpg");
        if (!File.Exists(testImagePath))
        {
            Console.WriteLine("Creating test image...");
            using var noise = Image.Gaussnoise(4000, 3000, mean: 128, sigma: 30);
            using var rgb = noise.Bandjoin(noise, noise);
            rgb.Jpegsave(testImagePath, q: 95);
        }

        outputDir = Path.Combine(Path.GetTempPath(), $"benchmark-format-{Guid.NewGuid():N}");
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
        if (Directory.Exists(outputDir))
        {
            foreach (var dir in Directory.GetDirectories(outputDir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private string[] GetFormats() => IncludeAvif
        ? ["jpg", "webp", "avif"]
        : ["jpg", "webp"];

    /// <summary>
    /// Current approach: For each image, generate all sizes × all formats.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void AllFormatsPerImage()
    {
        var formats = GetFormats();
        var workers = Math.Max(1, Environment.ProcessorCount / 2);

        NetVips.NetVips.Concurrency = Environment.ProcessorCount / 2;

        Parallel.For(0, ImageCount, new ParallelOptions { MaxDegreeOfParallelism = workers }, i =>
        {
            var imageDir = Path.Combine(outputDir, $"all_{i}");
            Directory.CreateDirectory(imageDir);

            using var original = Image.NewFromFile(testImagePath);

            foreach (var size in Sizes)
            {
                using var resized = original.ThumbnailImage(size);

                foreach (var format in formats)
                {
                    var outPath = Path.Combine(imageDir, $"{size}.{format}");
                    SaveImage(resized, outPath, format);
                }
            }
        });
    }

    /// <summary>
    /// Format-sequential: All images for JPG, then all for WebP, then all for AVIF.
    /// Uses Image.Thumbnail() per size.
    /// </summary>
    [Benchmark]
    public void FormatSequential()
    {
        var formats = GetFormats();

        foreach (var format in formats)
        {
            // Optimize concurrency per format
            int workers;
            int concurrency;

            if (format == "avif")
            {
                workers = Math.Max(1, Environment.ProcessorCount / 4);  // Fewer workers
                concurrency = Environment.ProcessorCount;  // Let libaom use threads
            }
            else
            {
                workers = Math.Max(1, Environment.ProcessorCount / 2);
                concurrency = Environment.ProcessorCount / 2;
            }

            NetVips.NetVips.Concurrency = concurrency;

            Parallel.For(0, ImageCount, new ParallelOptions { MaxDegreeOfParallelism = workers }, i =>
            {
                var imageDir = Path.Combine(outputDir, $"seq_{i}");
                Directory.CreateDirectory(imageDir);

                foreach (var size in Sizes)
                {
                    using var thumb = Image.Thumbnail(testImagePath, size, height: 10000000);
                    var outPath = Path.Combine(imageDir, $"{size}.{format}");
                    SaveImage(thumb, outPath, format);
                }
            });
        }
    }

    /// <summary>
    /// Format-sequential with same workers (no AVIF optimization).
    /// Control test to isolate the effect of format-sequential alone.
    /// </summary>
    [Benchmark]
    public void FormatSequentialSameWorkers()
    {
        var formats = GetFormats();
        var workers = Math.Max(1, Environment.ProcessorCount / 2);

        NetVips.NetVips.Concurrency = Environment.ProcessorCount / 2;

        foreach (var format in formats)
        {
            Parallel.For(0, ImageCount, new ParallelOptions { MaxDegreeOfParallelism = workers }, i =>
            {
                var imageDir = Path.Combine(outputDir, $"seqsame_{i}");
                Directory.CreateDirectory(imageDir);

                foreach (var size in Sizes)
                {
                    using var thumb = Image.Thumbnail(testImagePath, size, height: 10000000);
                    var outPath = Path.Combine(imageDir, $"{size}.{format}");
                    SaveImage(thumb, outPath, format);
                }
            });
        }
    }

    private static void SaveImage(Image image, string path, string format)
    {
        switch (format)
        {
            case "jpg":
                image.Jpegsave(path, q: 90, keep: Enums.ForeignKeep.None);
                break;
            case "webp":
                image.Webpsave(path, q: 85, keep: Enums.ForeignKeep.None);
                break;
            case "avif":
                image.Heifsave(path, q: 80, compression: Enums.ForeignHeifCompression.Av1,
                    keep: Enums.ForeignKeep.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported format");
        }
    }
}
