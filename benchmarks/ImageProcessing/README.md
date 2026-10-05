# Image Processing Benchmarks

Benchmarks for Revela's **production** image pipeline. They call the real
`NetVipsImageProcessor` and `ImageWorkerPlan` from `src/Features/Generate`
(internal types, visible through `InternalsVisibleTo` in `Generate.csproj`), so a
change to the pipeline shows up here without copying code.

## Running Benchmarks

```bash
cd benchmarks/ImageProcessing

# Run all benchmarks (1 warmup + 3 measured iterations each)
dotnet run -c Release -- --filter *

# Run one benchmark
dotnet run -c Release -- --filter *ProcessImage*
dotnet run -c Release -- --filter *ProcessBatch*
dotnet run -c Release -- --filter *ReadMetadata*

# Smoke check: every case once, no statistics
dotnet run -c Release -- --filter * --job dry

# List available benchmarks
dotnet run -c Release -- --list flat
```

Benchmarks run in-process (`--inProcess` is added automatically): BenchmarkDotNet's
out-of-process runner requires the `.csproj` file name to match the assembly name
(`Spectara.Revela.Benchmarks.ImageProcessing`). Each operation takes seconds, so the
in-process overhead is negligible.

The source photo is a deterministic, photo-like 6000×4000 JPEG (24 MP), generated once
into `%TEMP%/revela-benchmarks/` (`$TMPDIR` on Linux/macOS). Delete it to regenerate.

## Benchmarks

All benchmarks use the bundled Lumina sizes (160–2560 px, plus the full resolution)
and the qualities written by `revela config image` (jpg 90, webp 85, avif 75) with the
default encoder efforts.

| Benchmark | What it measures |
|-----------|------------------|
| **ProcessImageBenchmark** | One photo through `ProcessImageAsync` per format set (`jpg`, `webp`, `avif`, `jpg,webp`) |
| **ProcessBatchBenchmark** | Eight photos in parallel, scheduled like `ImageService` (`ImageWorkerPlan` workers and libvips threads per image), for `jpg,webp` and `avif,webp,jpg` |
| **ReadMetadataBenchmark** | The scan step (`ReadMetadataAsync`): dimensions, EXIF/XMP and the average-colour placeholder |

To compare an alternative strategy, change the production code on a branch and run the
same benchmark on both branches.
