using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Service for image processing (resize, convert, EXIF extraction).
/// </summary>
/// <remarks>
/// <para>
/// Processes images from the manifest, generating responsive variants
/// in multiple sizes and formats. An image is skipped when its processing
/// fingerprint (source size + modification time, resize mode, output version)
/// matches the one recorded after its last successful processing and all
/// expected variant files exist.
/// </para>
/// </remarks>
internal sealed partial class ImageService(
    IImageProcessor imageProcessor,
    IManifestRepository manifestRepository,
    IImageSizesProvider imageSizesProvider,
    IOptions<ProjectEnvironment> projectEnvironment,
    IPathResolver pathResolver,
    IOptionsMonitor<GenerateConfig> generateOptions,
    IArtifactLifecycle artifactLifecycle,
    TimeProvider timeProvider,
    ILogger<ImageService> logger) : IImageService
{
    /// <summary>Image output directory within output folder</summary>
    private const string ImageDirectory = "images";

    /// <summary>Save the manifest after this many finished images during a run.</summary>
    private const int CheckpointEveryImages = 25;

    /// <summary>Save the manifest at least this often during a run.</summary>
    private static readonly TimeSpan CheckpointInterval = TimeSpan.FromSeconds(30);

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
        var stopwatch = Stopwatch.StartNew();

        // Set once encoding starts: from then on, completed images are worth persisting
        // even if the run fails or is cancelled.
        var processingStarted = false;

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

            // A photo-less project (e.g. a calendar-only site) has no images to
            // process. Treat that as a successful no-op instead of failing on the
            // "no formats configured" guard below — a valid site may have zero images.
            var allImagePaths = CollectImagePaths(manifestRepository.Root);
            var uniqueSourcePaths = allImagePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (uniqueSourcePaths.Count == 0)
            {
                manifestRepository.RemoveOrphans(uniqueSourcePaths);
                manifestRepository.LastImagesProcessed = timeProvider.GetUtcNow().UtcDateTime;
                await manifestRepository.SaveAsync(cancellationToken);
                stopwatch.Stop();

                return new ImageResult
                {
                    Success = true,
                    ProcessedCount = 0,
                    SkippedCount = 0,
                    FilesCreated = 0,
                    TotalSize = 0,
                    Duration = stopwatch.Elapsed
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

            // Configured sizes, the fallback for images whose manifest entry has none
            var sizes = imageSizesProvider.GetSizes();

            // Detect which formats have quality changes (need regeneration)
            var savedQualities = manifestRepository.FormatQualities;
            var formatsWithQualityChange = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (format, quality) in formats)
            {
                if (savedQualities.TryGetValue(format, out var savedQuality) && savedQuality != quality)
                {
                    formatsWithQualityChange.Add(format);
                    LogQualityChanged(logger, format, savedQuality, quality);
                }
            }

            // Remove orphaned entries (unique paths were collected above)
            manifestRepository.RemoveOrphans(uniqueSourcePaths);

            // Determine which images need processing
            var imagesToProcess = new List<(string SourcePath, string ManifestKey, string ImageSlug, IReadOnlyList<int> Sizes, IReadOnlyList<(int Size, string Format)>? MissingVariants, string? ExistingPlaceholder, int Width, int Height, string Fingerprint)>();
            var cachedCount = 0;
            var resizeMode = imageSizesProvider.GetResizeMode();

            var selectionStopwatch = Stopwatch.StartNew();

            var outputImagesDirectory = Path.Combine(OutputPath, ImageDirectory);

            // Iterate over unique paths only (avoids double-counting filtered duplicates)
            foreach (var imagePath in uniqueSourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath = Path.Combine(SourcePath, imagePath);
                var fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Exists)
                {
                    continue;
                }

                var manifestKey = imagePath.Replace('\\', '/');
                var existingEntry = manifestRepository.GetImage(manifestKey);
                var imageName = UrlBuilder.ToImageSlug(manifestKey);

                // Get sizes from manifest (calculated during scan)
                var manifestSizes = existingEntry?.Sizes ?? [];

                // Get dimensions from manifest (required for processing)
                var width = existingEntry?.Width ?? 0;
                var height = existingEntry?.Height ?? 0;

                // Get existing placeholder from manifest (generated during scan)
                var existingPlaceholder = existingEntry?.Placeholder;

                // Change detection against the state recorded after the last successful
                // processing — never against scan metadata, which already reflects the edit.
                var fingerprint = ComputeProcessingFingerprint(fileInfo.Length, fileInfo.LastWriteTimeUtc, resizeMode);
                var sourceUnchanged = string.Equals(
                    manifestRepository.GetProcessedFingerprint(manifestKey),
                    fingerprint,
                    StringComparison.Ordinal);

                // Decision tree
                if (options.Force)
                {
                    // Force: regenerate everything
                    imagesToProcess.Add((fullPath, manifestKey, imageName, manifestSizes, null, existingPlaceholder, width, height, fingerprint));
                }
                else if (!sourceUnchanged)
                {
                    // Source or pipeline changed, or never processed: regenerate everything
                    imagesToProcess.Add((fullPath, manifestKey, imageName, manifestSizes, null, existingPlaceholder, width, height, fingerprint));
                }
                else
                {
                    // Source unchanged: check if all output files exist or quality changed
                    // This handles: config changes, quality changes, manually deleted files, interrupted builds
                    // Cost: ~10 File.Exists calls per image (cheap I/O, typically cached by OS)
                    var missingVariants = GetMissingVariants(outputImagesDirectory, imageName, manifestSizes, formats, formatsWithQualityChange);
                    if (missingVariants.Count > 0)
                    {
                        imagesToProcess.Add((fullPath, manifestKey, imageName, manifestSizes, missingVariants, existingPlaceholder, width, height, fingerprint));
                    }
                    else
                    {
                        cachedCount++;
                    }
                }
            }

            selectionStopwatch.Stop();
            LogSelectionCompleted(logger, uniqueSourcePaths.Count, imagesToProcess.Count, cachedCount, selectionStopwatch.Elapsed);

            long plannedVariants = 0;
            foreach (var (_, _, _, manifestSizes, missingVariants, _, _, _, _) in imagesToProcess)
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

            // Worker pool: CPU/2 images in parallel, each with a libvips concurrency capped at 8
            // (see NetVipsImageProcessor). This optimizes thread usage:
            // - Fewer workers = fewer parallel AVIF encoder instances (each spawns ~15 threads)
            // - Reduces total thread count by ~30% with equal or better performance
            var configuredParallelism = ImageSettings.MaxDegreeOfParallelism;
            var workerCount = configuredParallelism.HasValue
                ? Math.Max(1, configuredParallelism.Value)
                : Math.Max(1, Environment.ProcessorCount / 2);

            if (configuredParallelism.HasValue)
            {
                LogUsingConfiguredParallelism(logger, workerCount);
            }

            var formatNames = formats.Keys.ToList();

            if (imagesToProcess.Count == 0)
            {
                // Still save format qualities even when nothing to process
                // This initializes the qualities on first run or after manifest reset
                manifestRepository.SetFormatQualities(formats);
                manifestRepository.LastImagesProcessed = timeProvider.GetUtcNow().UtcDateTime;
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

            var invalidationResult = await artifactLifecycle.PrepareToReplaceAsync(
                CoreArtifacts.ProcessedImages,
                cancellationToken);
            if (!invalidationResult.Success)
            {
                return new ImageResult
                {
                    Success = false,
                    ErrorMessage = invalidationResult.ErrorMessage,
                    Duration = stopwatch.Elapsed
                };
            }

            // Process images in parallel with limited worker pool
            var cacheDirectory = Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache);
            var totalFilesCreated = 0;
            var totalSizeBytes = 0L;

            // Serializes manifest updates and checkpoint saves. A SemaphoreSlim (not a Lock)
            // because checkpoints await the manifest save.
            using var manifestGate = new SemaphoreSlim(1, 1);
            var imagesSinceCheckpoint = 0;
            var lastCheckpoint = stopwatch.Elapsed;

            // Lock-free shared counters that the encode workers update. A single
            // reporting path turns them into immutable snapshots for the UI — the
            // workers never render or take a render lock (the old worker grid did,
            // rebuilt per variant, which serialised every worker).
            var progressState = new ImageProgressState(imagesToProcess.Count, formatNames);

            // Rolling rate/ETA from a recent-time window: AVIF and JPG throughput
            // differ ~17×, so a naive average would promise a completion time the run
            // can never keep. Only touched from ReportProgress, serialised by rateLock.
            var rate = new ProgressRate(TimeSpan.FromSeconds(60));
            var rateLock = new Lock();

            void ReportProgress()
            {
                if (progress is null)
                {
                    return;
                }

                double perMinute;
                TimeSpan? eta;
                lock (rateLock)
                {
                    perMinute = rate.PerMinute ?? 0d;
                    eta = rate.Estimate(progressState.Total - progressState.Processed);
                }

                progress.Report(progressState.Snapshot(stopwatch.Elapsed, perMinute, eta));
            }

            ReportProgress();

            processingStarted = true;
            await Parallel.ForEachAsync(
                imagesToProcess,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = workerCount,
                    CancellationToken = cancellationToken
                },
                async (item, ct) =>
                {
                    var (sourcePath, manifestKey, imageSlug, manifestSizes, missingVariants, existingPlaceholder, width, height, fingerprint) = item;

                    // Use sizes from manifest (calculated during scan with original width).
                    // Fall back to config sizes if manifest sizes are empty (shouldn't happen).
                    // Sort ascending: smallest first so the display fills up immediately;
                    // the largest variant (slowest encode) runs last.
                    var sizesToGenerate = (manifestSizes.Count > 0 ? manifestSizes : sizes)
                        .OrderBy(s => s)
                        .ToList();

                    progressState.WorkerStarted();
                    ReportProgress();

                    try
                    {
                        var image = await imageProcessor.ProcessImageAsync(
                            sourcePath,
                            new ImageProcessingOptions
                            {
                                Formats = formats,
                                Sizes = sizesToGenerate,
                                VariantsToGenerate = missingVariants,  // null = all, list = incremental
                                OutputDirectory = outputImagesDirectory,
                                ImageSlug = imageSlug,
                                CacheDirectory = cacheDirectory,
                                ResizeMode = resizeMode,
                                Placeholder = ImageSettings.Placeholder,
                                ExistingPlaceholder = existingPlaceholder,
                                Width = width,
                                Height = height
                            },
                            // O(1), lock-free per-variant bookkeeping. No rendering and no
                            // display-state allocation — this runs tens of thousands of times.
                            onVariantProgress: (state, format) =>
                            {
                                switch (state)
                                {
                                    case VariantState.Started:
                                        break;

                                    case VariantState.Done:
                                        progressState.VariantDone(format);
                                        break;

                                    case VariantState.Skipped:
                                        progressState.VariantSkipped();
                                        break;

                                    default:
                                        throw new InvalidOperationException($"Unknown {nameof(VariantState)}: {state}");
                                }
                            },
                            ct);

                        // Count files created (actual variants) and accumulate size
                        var filesCreated = image.Variants.Count;
                        var imageSize = image.Variants.Sum(v => v.Size);

                        var currentProcessed = progressState.ImageCompleted();
                        Interlocked.Add(ref totalFilesCreated, filesCreated);
                        Interlocked.Add(ref totalSizeBytes, imageSize);

                        // Feed the rolling rate from the reporting path (serialised) and
                        // report a cheap snapshot — per finished image, not per variant.
                        lock (rateLock)
                        {
                            rate.Record(stopwatch.Elapsed, currentProcessed);
                        }

                        ReportProgress();

                        // Record success, update placeholder if changed, and checkpoint regularly
                        // so an interrupted run keeps the images it already finished.
                        // CancellationToken.None: a finished image must be recorded even if the
                        // run is being cancelled.
                        await manifestGate.WaitAsync(CancellationToken.None);
                        try
                        {
                            manifestRepository.SetProcessedFingerprint(manifestKey, fingerprint);

                            var existingEntry = manifestRepository.GetImage(manifestKey);
                            if (existingEntry != null && existingEntry.Placeholder != image.Placeholder)
                            {
                                manifestRepository.SetImage(manifestKey, existingEntry with
                                {
                                    Placeholder = image.Placeholder
                                });
                            }

                            imagesSinceCheckpoint++;
                            if (imagesSinceCheckpoint >= CheckpointEveryImages
                                || stopwatch.Elapsed - lastCheckpoint >= CheckpointInterval)
                            {
                                await manifestRepository.SaveAsync(CancellationToken.None);
                                imagesSinceCheckpoint = 0;
                                lastCheckpoint = stopwatch.Elapsed;
                            }
                        }
                        finally
                        {
                            manifestGate.Release();
                        }
                    }
                    finally
                    {
                        progressState.WorkerFinished();
                    }
                });

            // totalSizeBytes already accumulated from variants; skip directory scan

            // Save manifest with updated format qualities
            manifestRepository.SetFormatQualities(formats);
            manifestRepository.LastImagesProcessed = timeProvider.GetUtcNow().UtcDateTime;
            await manifestRepository.SaveAsync(cancellationToken);

            // Final snapshot at 100% for a clean end state before the summary panel.
            ReportProgress();

            LogImagesProcessed(logger, progressState.Processed);
            stopwatch.Stop();

            // Collect warnings from image processor
            var warnings = NetVipsImageProcessor.GetAndClearWarnings();

            return new ImageResult
            {
                Success = true,
                ProcessedCount = progressState.Processed,
                SkippedCount = cachedCount,
                FilesCreated = totalFilesCreated,
                TotalSize = totalSizeBytes,
                Duration = stopwatch.Elapsed,
                Warnings = warnings
            };
        }
        catch (OperationCanceledException)
        {
            if (processingStarted)
            {
                await SaveProgressAfterInterruptionAsync();
            }

            throw;
        }
        catch (Exception ex)
        {
            LogImageProcessingFailed(logger, ex);
            if (processingStarted)
            {
                await SaveProgressAfterInterruptionAsync();
            }

            return new ImageResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Persists the fingerprints of images finished before a run was interrupted.
    /// </summary>
    /// <remarks>
    /// Parallel.ForEachAsync has awaited all running workers before it throws, so no
    /// worker touches the manifest concurrently. Best effort: a failed save only costs
    /// re-encoding those images on the next run.
    /// </remarks>
    private async Task SaveProgressAfterInterruptionAsync()
    {
        try
        {
            await manifestRepository.SaveAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogProgressSaveFailed(logger, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Image> ProcessImageAsync(
        string inputPath,
        ImageProcessingOptions options,
        Action<VariantState, string>? onVariantProgress = null,
        CancellationToken cancellationToken = default) => await imageProcessor.ProcessImageAsync(inputPath, options, onVariantProgress, cancellationToken);

    #region Private Helpers

    /// <summary>
    /// Fingerprint of the inputs that determine an image's variants, apart from sizes and
    /// formats (checked per output file) and quality (tracked in <c>FormatQualities</c>).
    /// </summary>
    internal static string ComputeProcessingFingerprint(long fileSize, DateTime lastWriteTimeUtc, string resizeMode) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"v{NetVipsImageProcessor.OutputVersion}|size:{fileSize}|mtime:{lastWriteTimeUtc.Ticks}|resize:{resizeMode}");

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
    /// Checks each expected output file and returns those that:
    /// - Don't exist on disk (deleted, new size, new format)
    /// - Have quality changes (format exists but quality setting changed)
    /// This enables incremental generation when config changes.
    /// </remarks>
    private static List<(int Size, string Format)> GetMissingVariants(
        string outputDirectory,
        string imageName,
        IReadOnlyList<int> sizes,
        IReadOnlyDictionary<string, int> formats,
        HashSet<string> formatsWithQualityChange)
    {
        var missing = new List<(int Size, string Format)>();

        if (string.IsNullOrEmpty(imageName) || sizes.Count == 0 || formats.Count == 0)
        {
            // No valid image info - return empty (will be handled as "all missing")
            return missing;
        }

        var imageDirectory = Path.Combine(outputDirectory, imageName.Replace('/', Path.DirectorySeparatorChar));

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

                // Mark as missing if: file doesn't exist OR quality changed for this format
                if (!File.Exists(expectedPath) || formatsWithQualityChange.Contains(format))
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Quality changed for {Format}: {OldQuality} → {NewQuality}, regenerating all {Format} files")]
    private static partial void LogQualityChanged(ILogger logger, string format, int oldQuality, int newQuality);

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing failed")]
    private static partial void LogImageProcessingFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save image processing progress; finished images will be processed again on the next run")]
    private static partial void LogProgressSaveFailed(ILogger logger, Exception exception);

    #endregion
}

