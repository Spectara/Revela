using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Abstractions;
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

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Service for image processing (resize, convert, EXIF extraction).
/// </summary>
/// <remarks>
/// <para>
/// Processes images from the manifest, generating responsive variants
/// in multiple sizes and formats. An image is skipped when its processing
/// fingerprint (source size + modification time, resize mode, output version, applied maxSize cap)
/// matches the one recorded in <see cref="ImageStateStore"/> after its last successful
/// processing and every expected variant exists with the recorded quality and encoder effort of its format.
/// The scan manifest only supplies sizes, dimensions and placeholders, so rebuilding it
/// never re-encodes images.
/// </para>
/// </remarks>
internal sealed partial class ImageService(
    IImageProcessor imageProcessor,
    IManifestRepository manifestRepository,
    ImageStateStore imageState,
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

    /// <summary>Save the processing state after this many finished images during a run.</summary>
    private const int CheckpointEveryImages = 25;

    /// <summary>Thread-pool threads kept free beside the encode workers (progress, checkpoint saves).</summary>
    private const int ReservedPoolThreads = 4;

    /// <summary>Save the processing state at least this often during a run.</summary>
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
            // Load manifest (scan results) and processing state (what the variants on disk are made from)
            await manifestRepository.LoadAsync(cancellationToken);
            await imageState.LoadAsync(cancellationToken);

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
                imageState.RemoveExcept(uniqueSourcePaths);
                await imageState.SaveAsync(cancellationToken);
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

            // Remove orphaned entries (unique paths were collected above)
            manifestRepository.RemoveOrphans(uniqueSourcePaths);
            imageState.RemoveExcept(uniqueSourcePaths);

            // Determine which images need processing
            var imagesToProcess = new List<PendingImage>();
            var cachedCount = 0;
            var resizeMode = imageSizesProvider.GetResizeMode();
            var efforts = GetNonDefaultEfforts(formats, ImageSettings);
            var qualityChanges = new HashSet<(string Format, int Recorded, int Configured)>();
            var effortChanges = new HashSet<(string Format, int Recorded, int Configured)>();

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
                var appliedMaxSize = ImageSettings.MaxSize > 0
                    && NetVipsImageProcessor.GetResizeExtent(width, height, resizeMode) > ImageSettings.MaxSize
                        ? ImageSettings.MaxSize
                        : 0;
                var fingerprint = ComputeProcessingFingerprint(fileInfo.Length, fileInfo.LastWriteTimeUtc, resizeMode, appliedMaxSize);
                var recorded = imageState.Get(manifestKey);

                if (options.Force || recorded is null || !string.Equals(recorded.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    // Forced, source or pipeline changed, or never processed: regenerate everything.
                    // Variants of formats that are no longer configured are now stale, so only
                    // the configured qualities and efforts are recorded.
                    imagesToProcess.Add(new PendingImage(
                        fullPath, manifestKey, imageName, manifestSizes, null, existingPlaceholder, width, height,
                        new ProcessedImage
                        {
                            Fingerprint = fingerprint,
                            Qualities = new Dictionary<string, int>(formats),
                            Efforts = efforts.Count > 0 ? new Dictionary<string, int>(efforts) : null
                        }));
                    continue;
                }

                // Source unchanged: regenerate variants that are missing (deleted, new size or
                // format, interrupted run) or encoded with another quality or effort than configured.
                // Cost: ~10 File.Exists calls per image (cheap I/O, typically cached by OS)
                var staleFormats = GetStaleFormats(recorded, formats, efforts, qualityChanges, effortChanges);
                var missingVariants = GetMissingVariants(outputImagesDirectory, imageName, manifestSizes, formats, staleFormats);
                if (missingVariants.Count == 0)
                {
                    cachedCount++;
                    continue;
                }

                // Variants of formats that are no longer configured are untouched and still valid.
                var qualities = new Dictionary<string, int>(recorded.Qualities);
                var recordedEfforts = new Dictionary<string, int>(recorded.Efforts ?? new Dictionary<string, int>());
                foreach (var (format, quality) in formats)
                {
                    qualities[format] = quality;
                    if (efforts.TryGetValue(format, out var effort))
                    {
                        recordedEfforts[format] = effort;
                    }
                    else
                    {
                        recordedEfforts.Remove(format);
                    }
                }

                imagesToProcess.Add(new PendingImage(
                    fullPath, manifestKey, imageName, manifestSizes, missingVariants, existingPlaceholder, width, height,
                    recorded with { Qualities = qualities, Efforts = recordedEfforts.Count > 0 ? recordedEfforts : null }));
            }

            foreach (var (format, recordedQuality, configuredQuality) in qualityChanges)
            {
                LogQualityChanged(logger, format, recordedQuality, configuredQuality);
            }

            foreach (var (format, recordedEffort, configuredEffort) in effortChanges)
            {
                LogEffortChanged(logger, format, recordedEffort, configuredEffort);
            }

            selectionStopwatch.Stop();
            LogSelectionCompleted(logger, uniqueSourcePaths.Count, imagesToProcess.Count, cachedCount, selectionStopwatch.Elapsed);

            long plannedVariants = 0;
            foreach (var pending in imagesToProcess)
            {
                if (pending.MissingVariants != null)
                {
                    // Incremental mode: count only missing variants
                    plannedVariants += pending.MissingVariants.Count;
                }
                else
                {
                    // Full mode: all size/format combinations
                    var sizesToGenerateCount = pending.Sizes.Count > 0 ? pending.Sizes.Count : sizes.Count;
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

            // Images in parallel × libvips threads per image (see ImageWorkerPlan).
            var configuredParallelism = ImageSettings.MaxDegreeOfParallelism;
            var plan = ImageWorkerPlan.Create(
                Environment.ProcessorCount,
                GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                configuredParallelism,
                imagesToProcess.Count,
                encodesAvif: formats.ContainsKey("avif"));
            var workerCount = plan.Workers;

            if (configuredParallelism.HasValue)
            {
                LogUsingConfiguredParallelism(logger, workerCount);
            }

            LogWorkerPlan(logger, plan.Workers, plan.ThreadsPerImage);

            var formatNames = formats.Keys.ToList();

            if (imagesToProcess.Count == 0)
            {
                await imageState.SaveAsync(cancellationToken);
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

            // Serializes state updates and checkpoint saves. A SemaphoreSlim (not a Lock)
            // because checkpoints await the saves.
            using var checkpointGate = new SemaphoreSlim(1, 1);
            var imagesSinceCheckpoint = 0;
            var lastCheckpoint = stopwatch.Elapsed;
            var manifestChanged = false;

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

            // Each worker encodes synchronously on a thread-pool thread for a whole image. Keep
            // threads available beyond the workers, so progress reports and checkpoint saves never
            // wait for the pool to grow.
            NetVipsImageProcessor.SetThreadsPerImage(plan.ThreadsPerImage);
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minIoThreads);
            if (minWorkerThreads < workerCount + ReservedPoolThreads)
            {
                ThreadPool.SetMinThreads(workerCount + ReservedPoolThreads, minIoThreads);
            }

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
                    var (sourcePath, manifestKey, imageSlug, manifestSizes, missingVariants, existingPlaceholder, width, height, processed) = item;

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
                                Efforts = efforts,
                                Sizes = sizesToGenerate,
                                VariantsToGenerate = missingVariants,  // null = all, list = incremental
                                OutputDirectory = outputImagesDirectory,
                                ImageSlug = imageSlug,
                                CacheDirectory = cacheDirectory,
                                ResizeMode = resizeMode,
                                Placeholder = ImageSettings.Placeholder,
                                ExistingPlaceholder = existingPlaceholder,
                                Width = width,
                                Height = height,
                                MaxSize = ImageSettings.MaxSize
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
                        await checkpointGate.WaitAsync(CancellationToken.None);
                        try
                        {
                            imageState.Set(manifestKey, processed);

                            var existingEntry = manifestRepository.GetImage(manifestKey);
                            if (existingEntry != null && existingEntry.Placeholder != image.Placeholder)
                            {
                                manifestRepository.SetImage(manifestKey, existingEntry with
                                {
                                    Placeholder = image.Placeholder
                                });
                                manifestChanged = true;
                            }

                            imagesSinceCheckpoint++;
                            if (imagesSinceCheckpoint >= CheckpointEveryImages
                                || stopwatch.Elapsed - lastCheckpoint >= CheckpointInterval)
                            {
                                await imageState.SaveAsync(CancellationToken.None);
                                if (manifestChanged)
                                {
                                    await manifestRepository.SaveAsync(CancellationToken.None);
                                    manifestChanged = false;
                                }

                                imagesSinceCheckpoint = 0;
                                lastCheckpoint = stopwatch.Elapsed;
                            }
                        }
                        finally
                        {
                            checkpointGate.Release();
                        }
                    }
                    finally
                    {
                        progressState.WorkerFinished();
                    }
                });

            // totalSizeBytes already accumulated from variants; skip directory scan

            await imageState.SaveAsync(cancellationToken);
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
    /// Persists the state of images finished before a run was interrupted.
    /// </summary>
    /// <remarks>
    /// Parallel.ForEachAsync has awaited all running workers before it throws, so no
    /// worker touches the state concurrently. Best effort: a failed save only costs
    /// re-encoding those images on the next run.
    /// </remarks>
    private async Task SaveProgressAfterInterruptionAsync()
    {
        try
        {
            await imageState.SaveAsync(CancellationToken.None);
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
    /// Fingerprint of the inputs that determine all of an image's variants. Sizes and formats
    /// are checked per output file, and quality per format (<see cref="ProcessedImage.Qualities"/>).
    /// </summary>
    /// <remarks>
    /// A <c>maxSize</c> cap is included only for images it shrinks (<paramref name="appliedMaxSize"/>
    /// &gt; 0): their largest variant becomes a resize instead of the original, so they are
    /// re-encoded when the cap changes, while images within the cap and the default
    /// configuration keep their fingerprint.
    /// </remarks>
    internal static string ComputeProcessingFingerprint(long fileSize, DateTime lastWriteTimeUtc, string resizeMode, int appliedMaxSize = 0)
    {
        var fingerprint = string.Create(
            CultureInfo.InvariantCulture,
            $"v{NetVipsImageProcessor.OutputVersion}|size:{fileSize}|mtime:{lastWriteTimeUtc.Ticks}|resize:{resizeMode}");
        return appliedMaxSize > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{fingerprint}|max:{appliedMaxSize}")
            : fingerprint;
    }

    /// <summary>
    /// Encoder effort of the configured formats that differs from libvips' default.
    /// </summary>
    /// <remarks>
    /// Default efforts are left out, so a default configuration encodes and records exactly
    /// what it did before effort was configurable.
    /// </remarks>
    private static Dictionary<string, int> GetNonDefaultEfforts(IReadOnlyDictionary<string, int> formats, ImageConfig settings)
    {
        var efforts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var format in formats.Keys)
        {
            if (string.Equals(format, "avif", StringComparison.OrdinalIgnoreCase) && settings.AvifEffort != ImageConfig.DefaultAvifEffort)
            {
                efforts[format] = settings.AvifEffort;
            }
            else if (string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase) && settings.WebpEffort != ImageConfig.DefaultWebpEffort)
            {
                efforts[format] = settings.WebpEffort;
            }
        }

        return efforts;
    }

    /// <summary>
    /// Formats whose variants on disk were not encoded with the configured quality and effort.
    /// </summary>
    /// <remarks>
    /// A format without a recorded quality counts as stale: its files (if any) predate the
    /// recorded fingerprint, for example from before the format was last removed. A format
    /// without a recorded effort was encoded with the default effort.
    /// </remarks>
    private static HashSet<string> GetStaleFormats(
        ProcessedImage recorded,
        IReadOnlyDictionary<string, int> formats,
        IReadOnlyDictionary<string, int> efforts,
        HashSet<(string Format, int Recorded, int Configured)> qualityChanges,
        HashSet<(string Format, int Recorded, int Configured)> effortChanges)
    {
        var stale = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (format, quality) in formats)
        {
            if (!recorded.Qualities.TryGetValue(format, out var recordedQuality))
            {
                stale.Add(format);
            }
            else if (recordedQuality != quality)
            {
                stale.Add(format);
                qualityChanges.Add((format, recordedQuality, quality));
            }

            int? recordedEffort = recorded.Efforts?.TryGetValue(format, out var value) is true ? value : null;
            int? configuredEffort = efforts.TryGetValue(format, out var configured) ? configured : null;
            if (recordedEffort != configuredEffort)
            {
                stale.Add(format);
                effortChanges.Add((format, recordedEffort ?? DefaultEffort(format), configuredEffort ?? DefaultEffort(format)));
            }
        }

        return stale;
    }

    private static int DefaultEffort(string format) =>
        string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase) ? ImageConfig.DefaultWebpEffort : ImageConfig.DefaultAvifEffort;

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
    /// - Belong to a stale format (encoded with another quality than configured)
    /// This enables incremental generation when config changes.
    /// </remarks>
    private static List<(int Size, string Format)> GetMissingVariants(
        string outputDirectory,
        string imageName,
        IReadOnlyList<int> sizes,
        IReadOnlyDictionary<string, int> formats,
        HashSet<string> staleFormats)
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

                // Mark as missing if: file doesn't exist OR it was encoded with another quality
                if (!File.Exists(expectedPath) || staleFormats.Contains(format))
                {
                    missing.Add((size, format));
                }
            }
        }

        return missing;
    }

    #endregion

    /// <summary>
    /// An image selected for processing, with the state to record once its variants are written.
    /// </summary>
    /// <remarks><c>MissingVariants</c> is <c>null</c> to generate all variants.</remarks>
    private sealed record PendingImage(
        string SourcePath,
        string ManifestKey,
        string ImageSlug,
        IReadOnlyList<int> Sizes,
        IReadOnlyList<(int Size, string Format)>? MissingVariants,
        string? ExistingPlaceholder,
        int Width,
        int Height,
        ProcessedImage Processed);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Encoding {Workers} images in parallel with {Threads} libvips threads each")]
    private static partial void LogWorkerPlan(ILogger logger, int workers, int threads);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quality changed for {Format}: {OldQuality} → {NewQuality}, regenerating all {Format} files")]
    private static partial void LogQualityChanged(ILogger logger, string format, int oldQuality, int newQuality);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encoder effort changed for {Format}: {OldEffort} → {NewEffort}, regenerating all {Format} files")]
    private static partial void LogEffortChanged(ILogger logger, string format, int oldEffort, int newEffort);

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing failed")]
    private static partial void LogImageProcessingFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save image processing progress; finished images will be processed again on the next run")]
    private static partial void LogProgressSaveFailed(ILogger logger, Exception exception);

    #endregion
}

