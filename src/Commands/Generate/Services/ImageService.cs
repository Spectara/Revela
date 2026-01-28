using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

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
    IImageSizesProvider imageSizesProvider,
    IOptions<ProjectEnvironment> projectEnvironment,
    IPathResolver pathResolver,
    IOptionsMonitor<GenerateConfig> generateOptions,
    ILogger<ImageService> logger) : IImageService
{
    /// <summary>Image output directory within output folder</summary>
    private const string ImageDirectory = "images";

    /// <summary>Gets full path to source directory (supports hot-reload)</summary>
    private string SourcePath => pathResolver.SourcePath;

    /// <summary>Gets full path to output directory (supports hot-reload)</summary>
    private string OutputPath => pathResolver.OutputPath;

    /// <summary>Gets current image format settings (supports hot-reload)</summary>
    private ImageConfig ImageSettings => generateOptions.CurrentValue.Images;

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

            // Check if formats are configured
            var formats = ImageSettings.GetActiveFormats();
            if (formats.Count == 0)
            {
                return new ImageResult
                {
                    Success = false,
                    ErrorMessage = "No image formats configured. Run 'revela config image' first."
                };
            }

            // Store config hash for reference (but don't clear hashes on change)
            // Incremental generation handles missing variants per-image
            var sizes = imageSizesProvider.GetSizes();
            var configHash = ManifestService.ComputeConfigHash(sizes, formats);
            manifestRepository.ConfigHash = configHash;

            // Collect all unique image paths from the root tree
            // Note: The tree may contain duplicate paths (e.g., filtered images on homepage
            // that also exist in their original gallery). We use a HashSet to ensure each
            // image is only processed/counted once.
            var allImagePaths = CollectImagePaths(manifestRepository.Root);
            var uniqueSourcePaths = allImagePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Remove orphaned entries
            manifestRepository.RemoveOrphans(uniqueSourcePaths);

            // Determine which images need processing
            var imagesToProcess = new List<(string SourcePath, string Hash, string ManifestKey, IReadOnlyList<int> Sizes, IReadOnlyList<(int Size, string Format)>? MissingVariants, string? ExistingPlaceholder)>();
            var cachedCount = 0;

            var selectionStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var outputImagesDirectory = Path.Combine(OutputPath, ImageDirectory);

            // Iterate over unique paths only (avoids double-counting filtered duplicates)
            foreach (var imagePath in uniqueSourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath = Path.Combine(SourcePath, imagePath);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var sourceHash = fileHashService.ComputeHash(fullPath);
                var manifestKey = imagePath.Replace('\\', '/');
                var existingEntry = manifestRepository.GetImage(manifestKey);
                var imageName = Path.GetFileNameWithoutExtension(existingEntry?.Filename ?? "");

                // Get sizes from manifest (calculated during scan)
                var manifestSizes = existingEntry?.Sizes ?? [];

                // Check if source hash changed - forces full regeneration
                var sourceChanged = options.Force || ManifestService.NeedsProcessing(existingEntry, sourceHash);

                // Get existing placeholder from manifest (generated during scan)
                var existingPlaceholder = existingEntry?.Placeholder;

                if (sourceChanged)
                {
                    // Source changed or forced: regenerate all variants
                    imagesToProcess.Add((fullPath, sourceHash, manifestKey, manifestSizes, null, existingPlaceholder));
                }
                else
                {
                    // Source unchanged: check which variants are missing
                    var missingVariants = GetMissingVariants(outputImagesDirectory, imageName, manifestSizes, formats);

                    if (missingVariants.Count > 0)
                    {
                        // Incremental: only generate missing variants
                        imagesToProcess.Add((fullPath, sourceHash, manifestKey, manifestSizes, missingVariants, existingPlaceholder));
                    }
                    else
                    {
                        // All variants exist - skip
                        cachedCount++;
                    }
                }
            }

            selectionStopwatch.Stop();
            LogSelectionCompleted(logger, uniqueSourcePaths.Count, imagesToProcess.Count, cachedCount, selectionStopwatch.Elapsed);

            long plannedVariants = 0;
            foreach (var (_, _, _, manifestSizes, missingVariants, _) in imagesToProcess)
            {
                if (missingVariants != null)
                {
                    // Incremental mode: count only missing variants
                    plannedVariants += missingVariants.Count;
                }
                else
                {
                    // Full mode: all size/format combinations
                    var sizesToGenerateCount = manifestSizes.Count > 0 ? manifestSizes.Count : sizes.Count;
                    plannedVariants += (long)sizesToGenerateCount * formats.Count;
                }
            }

            if (imagesToProcess.Count > 0)
            {
                LogVariantsPlanned(logger, imagesToProcess.Count, plannedVariants, formats.Count);
            }

            if (cachedCount > 0)
            {
                LogCacheHits(logger, cachedCount, uniqueSourcePaths.Count);
            }

            // Worker pool configuration (configurable)
            var configuredParallelism = ImageSettings.MaxDegreeOfParallelism;
            var workerCount = configuredParallelism.HasValue
                ? Math.Max(1, configuredParallelism.Value)
                : Math.Max(1, Environment.ProcessorCount - 2);

            if (configuredParallelism.HasValue)
            {
                LogUsingConfiguredParallelism(logger, workerCount);
            }

            // Thread-safe worker state storage for progress display
            var workerStates = new ConcurrentDictionary<int, WorkerState>();
            for (var i = 0; i < workerCount; i++)
            {
                workerStates[i] = new WorkerState { WorkerId = i };
            }

            // Report initial progress
            progress?.Report(new ImageProgress
            {
                Processed = 0,
                Total = imagesToProcess.Count,
                Skipped = cachedCount,
                Workers = [.. workerStates.Values.OrderBy(w => w.WorkerId)]
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

            // Process images in parallel with limited worker pool
            var cacheDirectory = Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache);
            var processedCount = 0;
            var totalFilesCreated = 0;
            var totalSizeBytes = 0L;
            var manifestLock = new object();

            // Track which worker is processing each image
            var workerAssignments = new ConcurrentDictionary<int, int>(); // taskId -> workerId
            var nextWorkerId = 0;

            await Parallel.ForEachAsync(
                imagesToProcess,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = workerCount,
                    CancellationToken = cancellationToken
                },
                async (item, ct) =>
                {
                    var (sourcePath, sourceHash, manifestKey, manifestSizes, missingVariants, existingPlaceholder) = item;

                    // Assign a worker ID to this task
                    var taskId = Environment.CurrentManagedThreadId;
                    var workerId = workerAssignments.GetOrAdd(taskId, _ => Interlocked.Increment(ref nextWorkerId) - 1) % workerCount;

                    // Use sizes from manifest (calculated during scan with original width)
                    // Fall back to config sizes if manifest sizes are empty (shouldn't happen)
                    // Sort descending: largest first (heavy work first, then quick small sizes)
                    var sizesToGenerate = (manifestSizes.Count > 0 ? manifestSizes : sizes)
                        .OrderByDescending(s => s)
                        .ToList();

                    // Calculate total variants for this image
                    // In incremental mode: total = all possible, but only missing will be generated
                    var variantsTotal = sizesToGenerate.Count * formats.Count;
                    var variantsDone = 0;
                    var variantsSkipped = 0;
                    var variantResults = new List<VariantResult>();

                    // Update worker state: starting new image
                    workerStates[workerId] = new WorkerState
                    {
                        WorkerId = workerId,
                        ImageName = Path.GetFileName(sourcePath),
                        VariantsTotal = variantsTotal,
                        VariantsDone = 0,
                        VariantsSkipped = 0,
                        VariantResults = []
                    };

                    // Report progress with updated worker state
                    progress?.Report(new ImageProgress
                    {
                        Processed = processedCount,
                        Total = imagesToProcess.Count,
                        Skipped = cachedCount,
                        Workers = [.. workerStates.Values.OrderBy(w => w.WorkerId)]
                    });

                    var image = await imageProcessor.ProcessImageAsync(
                        sourcePath,
                        new ImageProcessingOptions
                        {
                            Formats = formats,
                            Sizes = sizesToGenerate,
                            VariantsToGenerate = missingVariants,  // null = all, list = incremental
                            OutputDirectory = outputImagesDirectory,
                            CacheDirectory = cacheDirectory,
                            ResizeMode = imageSizesProvider.GetResizeMode(),
                            Placeholder = ImageSettings.Placeholder,
                            ExistingPlaceholder = existingPlaceholder
                        },
                        onVariantSaved: skipped =>
                        {
                            // Track result in order and update counters
                            if (skipped)
                            {
                                Interlocked.Increment(ref variantsSkipped);
                                lock (variantResults)
                                {
                                    variantResults.Add(VariantResult.Skipped);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref variantsDone);
                                lock (variantResults)
                                {
                                    variantResults.Add(VariantResult.Done);
                                }
                            }

                            // Update worker state with results list
                            List<VariantResult> resultsCopy;
                            lock (variantResults)
                            {
                                resultsCopy = [.. variantResults];
                            }

                            workerStates[workerId] = new WorkerState
                            {
                                WorkerId = workerId,
                                ImageName = Path.GetFileName(sourcePath),
                                VariantsTotal = variantsTotal,
                                VariantsDone = variantsDone,
                                VariantsSkipped = variantsSkipped,
                                VariantResults = resultsCopy
                            };

                            // Report progress
                            progress?.Report(new ImageProgress
                            {
                                Processed = processedCount,
                                Total = imagesToProcess.Count,
                                Skipped = cachedCount,
                                Workers = [.. workerStates.Values.OrderBy(w => w.WorkerId)]
                            });
                        },
                        ct);

                    // Count files created (actual variants) and accumulate size
                    var filesCreated = image.Variants.Count;
                    var imageSize = image.Variants.Sum(v => v.Size);

                    // Thread-safe updates
                    var currentProcessed = Interlocked.Increment(ref processedCount);
                    Interlocked.Add(ref totalFilesCreated, filesCreated);
                    Interlocked.Add(ref totalSizeBytes, imageSize);

                    // Mark worker as idle
                    workerStates[workerId] = new WorkerState { WorkerId = workerId };

                    // Report final progress for this image
                    progress?.Report(new ImageProgress
                    {
                        Processed = currentProcessed,
                        Total = imagesToProcess.Count,
                        Skipped = cachedCount,
                        Workers = [.. workerStates.Values.OrderBy(w => w.WorkerId)]
                    });

                    // Thread-safe manifest update - update ProcessedAt, Hash, and Placeholder
                    lock (manifestLock)
                    {
                        var existingEntry = manifestRepository.GetImage(manifestKey);
                        if (existingEntry != null)
                        {
                            manifestRepository.SetImage(manifestKey, existingEntry with
                            {
                                Hash = sourceHash,
                                ProcessedAt = DateTime.UtcNow,
                                Placeholder = image.Placeholder
                            });
                        }
                    }
                });

            // totalSizeBytes already accumulated from variants; skip directory scan

            // Save manifest
            manifestRepository.LastImagesProcessed = DateTime.UtcNow;
            await manifestRepository.SaveAsync(cancellationToken);

            // Final progress - all workers idle but keep row count stable
            progress?.Report(new ImageProgress
            {
                Processed = processedCount,
                Total = imagesToProcess.Count,
                Skipped = cachedCount,
                Workers = [.. workerStates.Values.OrderBy(w => w.WorkerId)]
            });

            LogImagesProcessed(logger, processedCount);
            stopwatch.Stop();

            // Collect warnings from image processor
            var warnings = NetVipsImageProcessor.GetAndClearWarnings();

            return new ImageResult
            {
                Success = true,
                ProcessedCount = processedCount,
                SkippedCount = cachedCount,
                FilesCreated = totalFilesCreated,
                TotalSize = totalSizeBytes,
                Duration = stopwatch.Elapsed,
                Warnings = warnings
            };
        }
        catch (OperationCanceledException)
        {
            throw;
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
        Action<bool>? onVariantSaved = null,
        CancellationToken cancellationToken = default) => await imageProcessor.ProcessImageAsync(inputPath, options, onVariantSaved, cancellationToken);

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
            // Use SourcePath if available (for filtered images from _images)
            // Fall back to entry.Path + Filename for backward compatibility
            var sourcePath = !string.IsNullOrEmpty(image.SourcePath)
                ? image.SourcePath
                : string.IsNullOrEmpty(entry.Path)
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

    #region Incremental Generation

    /// <summary>
    /// Get list of missing size/format combinations for an image.
    /// </summary>
    /// <remarks>
    /// Checks each expected output file and returns those that don't exist.
    /// This enables incremental generation when config changes (e.g., new size added).
    /// </remarks>
    private static List<(int Size, string Format)> GetMissingVariants(
        string outputDirectory,
        string imageName,
        IReadOnlyList<int> sizes,
        IReadOnlyDictionary<string, int> formats)
    {
        var missing = new List<(int Size, string Format)>();

        if (string.IsNullOrEmpty(imageName) || sizes.Count == 0 || formats.Count == 0)
        {
            // No valid image info - return empty (will be handled as "all missing")
            return missing;
        }

        var imageDirectory = Path.Combine(outputDirectory, imageName);

        // If image directory doesn't exist, all variants are missing
        if (!Directory.Exists(imageDirectory))
        {
            foreach (var size in sizes)
            {
                foreach (var format in formats.Keys)
                {
                    missing.Add((size, format));
                }
            }

            return missing;
        }

        // Check each size/format combination
        foreach (var size in sizes)
        {
            foreach (var format in formats.Keys)
            {
                var expectedPath = Path.Combine(imageDirectory, $"{size}.{format}");
                if (!File.Exists(expectedPath))
                {
                    missing.Add((size, format));
                }
            }
        }

        return missing;
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Using cached data for {CacheHits}/{Total} images")]
    private static partial void LogCacheHits(ILogger logger, int cacheHits, int total);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed {Count} images")]
    private static partial void LogImagesProcessed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Image selection: {Total} total, {ToProcess} to process, {Cached} cached in {Duration}")]
    private static partial void LogSelectionCompleted(ILogger logger, int total, int toProcess, int cached, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Planned processing: {ImageCount} images, {VariantCount} variants ({FormatCount} formats)")]
    private static partial void LogVariantsPlanned(ILogger logger, int imageCount, long variantCount, int formatCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Output size scan: {FileCount} files, {Bytes} bytes in {Duration}")]
    private static partial void LogOutputSizeScanned(ILogger logger, int fileCount, long bytes, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using configured parallelism for images: {Workers}")]
    private static partial void LogUsingConfiguredParallelism(ILogger logger, int workers);

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing failed")]
    private static partial void LogImageProcessingFailed(ILogger logger, Exception exception);

    #endregion
}
