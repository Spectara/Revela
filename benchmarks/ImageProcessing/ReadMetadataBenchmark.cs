using BenchmarkDotNet.Attributes;
using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Benchmarks.ImageProcessing;

/// <summary>
/// The scan step of the production <see cref="NetVipsImageProcessor"/>: dimensions, EXIF/XMP and
/// the average-colour placeholder for one photo.
/// </summary>
[MemoryDiagnoser]
public class ReadMetadataBenchmark
{
    private NetVipsImageProcessor processor = null!;
    private string sourcePath = null!;

    [GlobalSetup]
    public void Setup()
    {
        processor = ImageBenchmarkFixture.CreateProcessor();
        sourcePath = ImageBenchmarkFixture.GetTestImage(6000, 4000);
    }

    [Benchmark]
    public async Task<int> ReadMetadata()
    {
        var metadata = await processor.ReadMetadataAsync(sourcePath);
        return metadata.Width;
    }
}
