using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Manifest;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Service for image processing (resize, convert, EXIF extraction).
/// </summary>
/// <remarks>
/// <para>
/// Processes images from the manifest, generating responsive variants
/// in multiple sizes and formats. Uses hash-based caching to skip
/// unchanged images.
/// </para>
/// </remarks>
public sealed partial class ImageService(
    IImageProcessor imageProcessor,
    IFileHashService fileHashService,
    IManifestRepository manifestRepository,
    IOptions<RevelaConfig> options,
    ILogger<ImageService> logger) : IImageService
{
    /// <summary>Fixed source directory (convention over configuration)</summary>
    private const string SourceDirectory = "source";

    /// <summary>Output directory for generated site</summary>
    private const string OutputDirectory = "output";

    /// <summary>Image output directory within output folder</summary>
    private const string ImageDirectory = "images";

    /// <summary>Cache directory name</summary>
    private const string CacheDirectory = ".cache";

    /// <summary>Image settings from configuration</summary>
    private readonly ImageSettings imageSettings = options.Value.Generate.Images;

    /// <inheritdoc />
    public async Task<ImageResult> ProcessAsync(
        ProcessImagesOptions options,
        IProgress<ImageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Load manifest
            await manifestRepository.LoadAsync(cancellationToken);

            if (manifestRepository.Root is null)
            {
                return new ImageResult
                {
                    Success = false,
                    ErrorMessage = "No content in manifest. Run scan first."
                };
            }

            // Check if config changed (forces full rebuild)
            var sizes = imageSettings.Sizes.ToArray();
            var formats = imageSettings.Formats.Count > 0
                ? imageSettings.Formats
                : ImageSettings.DefaultFormats;
            var configHash = ManifestService.ComputeConfigHash(sizes, formats);
            var configChanged = manifestRepository.ConfigHash != configHash;

            if (configChanged && manifestRepository.Images.Count > 0)
            {
                LogConfigChanged(logger);
                manifestRepository.Clear();
                await manifestRepository.LoadAsync(cancellationToken); // Reload to get content back
            }

            manifestRepository.ConfigHash = configHash;

            // Collect all image paths from the root tree
            var allImagePaths = CollectImagePaths(manifestRepository.Root);
            var currentSourcePaths = allImagePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Remove orphaned entries
            manifestRepository.RemoveOrphans(currentSourcePaths);

            // Determine which images need processing
            var imagesToProcess = new List<(string SourcePath, string Hash, string ManifestKey, IReadOnlyList<int> Sizes)>();
            var cachedCount = 0;

            foreach (var imagePath in allImagePaths)
            {
                var fullPath = Path.Combine(SourceDirectory, imagePath);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var sourceHash = fileHashService.ComputeHash(fullPath);
                var manifestKey = imagePath.Replace('\\', '/');
                var existingEntry = manifestRepository.GetImage(manifestKey);

                if (!options.Force && !ManifestService.NeedsProcessing(existingEntry, sourceHash))
                {
                    cachedCount++;
                }
                else
                {
                    // Get sizes from manifest (calculated during scan)
                    var manifestSizes = existingEntry?.Sizes ?? [];
                    imagesToProcess.Add((fullPath, sourceHash, manifestKey, manifestSizes));
                }
            }

            if (cachedCount > 0)
            {
                LogCacheHits(logger, cachedCount, allImagePaths.Count);
            }

            // Report initial progress
            progress?.Report(new ImageProgress
            {
                CurrentImage = string.Empty,
                Processed = 0,
                Total = imagesToProcess.Count,
                Skipped = cachedCount
            });

            if (imagesToProcess.Count == 0)
            {
                manifestRepository.LastImagesProcessed = DateTime.UtcNow;
                await manifestRepository.SaveAsync(cancellationToken);
                stopwatch.Stop();

                return new ImageResult
                {
                    Success = true,
                    ProcessedCount = 0,
                    SkippedCount = cachedCount,
                    FilesCreated = 0,
                    TotalSize = 0,
                    Duration = stopwatch.Elapsed
                };
            }

            // Process images in parallel
            // Uses Environment.ProcessorCount by default (optimal for CPU-bound work)
            var outputImagesDirectory = Path.Combine(OutputDirectory, ImageDirectory);
            var cacheDirectory = Path.Combine(Environment.CurrentDirectory, CacheDirectory);
            var processedCount = 0;
            var totalFilesCreated = 0;
            var totalSizeBytes = 0L;
            var manifestLock = new object();

            await Parallel.ForEachAsync(
                imagesToProcess,
                cancellationToken,
                async (item, ct) =>
                {
                    var (sourcePath, sourceHash, manifestKey, manifestSizes) = item;

                    // Use sizes from manifest (calculated during scan with original width)
                    // Fall back to config sizes if manifest sizes are empty (shouldn't happen)
                    var sizesToGenerate = manifestSizes.Count > 0 ? manifestSizes : sizes;

                    var image = await imageProcessor.ProcessImageAsync(
                        sourcePath,
                        new ImageProcessingOptions
                        {
                            Formats = formats,
                            Sizes = sizesToGenerate,
                            OutputDirectory = outputImagesDirectory,
                            CacheDirectory = cacheDirectory
                        },
                        ct);

                    // Count files created: sizes × formats
                    var filesCreated = image.Sizes.Count * formats.Count;

                    // Thread-safe updates
                    var currentProcessed = Interlocked.Increment(ref processedCount);
                    Interlocked.Add(ref totalFilesCreated, filesCreated);

                    // Report progress (Progress<T> is thread-safe)
                    progress?.Report(new ImageProgress
                    {
                        CurrentImage = Path.GetFileName(sourcePath),
                        Processed = currentProcessed,
                        Total = imagesToProcess.Count,
                        Skipped = cachedCount
                    });

                    // Thread-safe manifest update - only update ProcessedAt timestamp
                    lock (manifestLock)
                    {
                        var existingEntry = manifestRepository.GetImage(manifestKey);
                        if (existingEntry != null)
                        {
                            manifestRepository.SetImage(manifestKey, existingEntry with
                            {
                                Hash = sourceHash,
                                ProcessedAt = DateTime.UtcNow
                            });
                        }
                    }
                });

            // Calculate total size of output files
            if (Directory.Exists(outputImagesDirectory))
            {
                totalSizeBytes = Directory.EnumerateFiles(outputImagesDirectory, "*.*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }

            // Save manifest
            manifestRepository.LastImagesProcessed = DateTime.UtcNow;
            await manifestRepository.SaveAsync(cancellationToken);

            // Final progress
            progress?.Report(new ImageProgress
            {
                CurrentImage = string.Empty,
                Processed = processedCount,
                Total = imagesToProcess.Count,
                Skipped = cachedCount
            });

            LogImagesProcessed(logger, processedCount);
            stopwatch.Stop();

            return new ImageResult
            {
                Success = true,
                ProcessedCount = processedCount,
                SkippedCount = cachedCount,
                FilesCreated = totalFilesCreated,
                TotalSize = totalSizeBytes,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            LogImageProcessingFailed(logger, ex);
            return new ImageResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <inheritdoc />
    public async Task<Image> ProcessImageAsync(
        string inputPath,
        ImageProcessingOptions options,
        CancellationToken cancellationToken = default) => await imageProcessor.ProcessImageAsync(inputPath, options, cancellationToken);

    #region Private Helpers

    /// <summary>
    /// Collect all image source paths from the unified tree.
    /// </summary>
    private static List<string> CollectImagePaths(ManifestEntry root)
    {
        var paths = new List<string>();
        CollectImagePathsRecursive(root, paths);
        return paths;
    }

    private static void CollectImagePathsRecursive(ManifestEntry entry, List<string> paths)
    {
        // Collect images from this node
        foreach (var image in entry.Content.OfType<ImageContent>())
        {
            // Use forward slashes for cross-platform consistency (matches imageCache keys)
            var sourcePath = string.IsNullOrEmpty(entry.Path)
                ? image.Filename
                : $"{entry.Path}/{image.Filename}";
            // Normalize any remaining backslashes from entry.Path
            paths.Add(sourcePath.Replace('\\', '/'));
        }

        // Recurse into children
        foreach (var child in entry.Children)
        {
            CollectImagePathsRecursive(child, paths);
        }
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Config changed, rebuilding all images")]
    private static partial void LogConfigChanged(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using cached data for {CacheHits}/{Total} images")]
    private static partial void LogCacheHits(ILogger logger, int cacheHits, int total);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed {Count} images")]
    private static partial void LogImagesProcessed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing failed")]
    private static partial void LogImageProcessingFailed(ILogger logger, Exception exception);

    #endregion
}
