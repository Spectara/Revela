using BenchmarkDotNet.Attributes;
using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Benchmarks.ImageProcessing;

/// <summary>
/// One photo through the production <see cref="NetVipsImageProcessor"/>: Lumina sizes plus the
/// full resolution, encoded to each format with the default qualities and efforts.
/// </summary>
[MemoryDiagnoser]
public class ProcessImageBenchmark
{
    private NetVipsImageProcessor processor = null!;
    private string sourcePath = null!;
    private string outputDirectory = null!;
    private IReadOnlyDictionary<string, int> formats = null!;

    /// <summary>
    /// Output formats (comma-separated) encoded with the <c>revela config image</c> qualities.
    /// </summary>
    [Params("jpg", "webp", "avif", "jpg,webp")]
    public string Formats { get; set; } = "jpg";

    // 24 MP, a typical modern camera.
    private const int Width = 6000;
    private const int Height = 4000;

    [GlobalSetup]
    public void Setup()
    {
        processor = ImageBenchmarkFixture.CreateProcessor();
        sourcePath = ImageBenchmarkFixture.GetTestImage(Width, Height);
        outputDirectory = ImageBenchmarkFixture.CreateOutputDirectory("process");
        formats = ImageBenchmarkFixture.ParseFormats(Formats);
        ImageBenchmarkFixture.ApplyWorkerPlan(imageCount: 1, formats);
    }

    [IterationSetup]
    public void IterationSetup() => ImageBenchmarkFixture.ClearDirectory(outputDirectory);

    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(outputDirectory, recursive: true);

    [Benchmark]
    public async Task<int> ProcessImage()
    {
        var options = ImageBenchmarkFixture.CreateOptions(formats, outputDirectory, "photo", Width, Height);
        var image = await processor.ProcessImageAsync(sourcePath, options);
        return image.Variants.Count;
    }
}
