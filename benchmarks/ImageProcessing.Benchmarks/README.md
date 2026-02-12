# Image Processing Benchmarks

Benchmarks for comparing different image processing strategies in Revela.

## Running Benchmarks

```bash
# Run all benchmarks
cd benchmarks/ImageProcessing.Benchmarks
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release -- --filter *ResizeStrategy*
dotnet run -c Release -- --filter *FormatSequential*

# Quick test run (fewer iterations)
dotnet run -c Release -- --filter *ResizeStrategy* --job short

# List available benchmarks
dotnet run -c Release -- --list flat
```

## Benchmarks

### ResizeStrategyBenchmark

Compares resize strategies for generating multiple image sizes:

| Strategy | Description |
|----------|-------------|
| **StarFromOriginal** | Load original once with `NewFromFile()`, call `Resize()` for each size |
| **ThumbnailPerSize** | Call `Image.Thumbnail()` for each size (libvips recommended) |
| **ThumbnailThenResize** | `Thumbnail()` to largest size, then `Resize()` for smaller |

### FormatSequentialBenchmark

Compares processing order for multiple formats (JPG, WebP, AVIF):

| Strategy | Description |
|----------|-------------|
| **AllFormatsPerImage** | Process all formats for each image before moving on |
| **FormatSequential** | All images → JPG, then all → WebP, then all → AVIF |
| **FormatSequentialSameWorkers** | Format-sequential without AVIF worker optimization |

## Expected Results

Based on libvips maintainer recommendations and our use case:

1. **StarFromOriginal** should be competitive because:
   - We always need original size (for lightbox)
   - No shrink-on-load benefit when loading full resolution
   - Single disk read vs multiple

2. **FormatSequential** may help because:
   - Better CPU cache locality (same encoder code stays hot)
   - AVIF can use different parallelism settings
   - OS file cache benefits subsequent format phases
