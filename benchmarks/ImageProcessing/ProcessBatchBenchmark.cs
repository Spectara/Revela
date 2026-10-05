using System.Globalization;
using BenchmarkDotNet.Attributes;
using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Benchmarks.ImageProcessing;

/// <summary>
/// A batch of photos through the production <see cref="NetVipsImageProcessor"/>, scheduled like
/// <c>ImageService</c>: <see cref="ImageWorkerPlan"/> picks the parallel images and the libvips
/// threads per image.
/// </summary>
[MemoryDiagnoser]
public class ProcessBatchBenchmark
{
    private NetVipsImageProcessor processor = null!;
    private string sourcePath = null!;
    private string outputDirectory = null!;
    private IReadOnlyDictionary<string, int> formats = null!;
    private int workers;

    /// <summary>
    /// Output formats (comma-separated) encoded with the <c>revela config image</c> qualities.
    /// </summary>
    [Params("jpg,webp", "avif,webp,jpg")]
    public string Formats { get; set; } = "jpg,webp";

    private const int ImageCount = 8;
    private const int Width = 6000;
    private const int Height = 4000;

    [GlobalSetup]
    public void Setup()
    {
        processor = ImageBenchmarkFixture.CreateProcessor();
        sourcePath = ImageBenchmarkFixture.GetTestImage(Width, Height);
        outputDirectory = ImageBenchmarkFixture.CreateOutputDirectory("batch");
        formats = ImageBenchmarkFixture.ParseFormats(Formats);
        workers = ImageBenchmarkFixture.ApplyWorkerPlan(ImageCount, formats);
        Console.WriteLine($"// Worker plan: {workers} parallel images");
    }

    [IterationSetup]
    public void IterationSetup() => ImageBenchmarkFixture.ClearDirectory(outputDirectory);

    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(outputDirectory, recursive: true);

    [Benchmark]
    public async Task<int> ProcessBatch()
    {
        var variants = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(0, ImageCount),
            new ParallelOptions { MaxDegreeOfParallelism = workers },
            async (index, cancellationToken) =>
            {
                var slug = index.ToString("D3", CultureInfo.InvariantCulture);
                var options = ImageBenchmarkFixture.CreateOptions(formats, outputDirectory, slug, Width, Height);
                var image = await processor.ProcessImageAsync(sourcePath, options, cancellationToken: cancellationToken);
                Interlocked.Add(ref variants, image.Variants.Count);
            });
        return variants;
    }
}
