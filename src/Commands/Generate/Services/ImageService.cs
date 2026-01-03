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
    IOptionsMonitor<GenerateConfig> generateOptions,
    ILogger<ImageService> logger) : IImageService
{
    /// <summary>Image output directory within output folder</summary>
    private const string ImageDirectory = "images";

    /// <summary>Gets full path to source directory</summary>
    private string SourcePath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Source);

    /// <summary>Gets full path to output directory</summary>
    private string OutputPath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Output);

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

            // Check if config changed (forces full rebuild)
            var sizes = imageSizesProvider.GetSizes();
            var formats = ImageSettings.GetActiveFormats();
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

            var selectionStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var outputImagesDirectory = Path.Combine(OutputPath, ImageDirectory);

            foreach (var imagePath in allImagePaths)
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

                // Check if output files exist (cache is only valid if outputs exist)
                var imageName = Path.GetFileNameWithoutExtension(existingEntry?.Filename ?? "");
                var outputExists = existingEntry?.Sizes.Count > 0 &&
                    OutputFilesExist(outputImagesDirectory, imageName, existingEntry.Sizes, formats);

                if (!options.Force && outputExists && !ManifestService.NeedsProcessing(existingEntry, sourceHash))
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

            selectionStopwatch.Stop();
            LogSelectionCompleted(logger, allImagePaths.Count, imagesToProcess.Count, cachedCount, selectionStopwatch.Elapsed);

            long plannedVariants = 0;
            foreach (var (_, _, _, manifestSizes) in imagesToProcess)
            {
                var sizesToGenerateCount = manifestSizes.Count > 0 ? manifestSizes.Count : sizes.Count;
                plannedVariants += (long)sizesToGenerateCount * formats.Count;
            }

            if (imagesToProcess.Count > 0)
            {
                LogVariantsPlanned(logger, imagesToProcess.Count, plannedVariants, formats.Count);
            }

            if (cachedCount > 0)
            {
                LogCacheHits(logger, cachedCount, allImagePaths.Count);
            }

            // Worker pool configuration
            var workerCount = Math.Max(1, Environment.ProcessorCount - 2);

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
                    var (sourcePath, sourceHash, manifestKey, manifestSizes) = item;

                    // Assign a worker ID to this task
                    var taskId = Environment.CurrentManagedThreadId;
                    var workerId = workerAssignments.GetOrAdd(taskId, _ => Interlocked.Increment(ref nextWorkerId) - 1) % workerCount;

                    // Use sizes from manifest (calculated during scan with original width)
                    // Fall back to config sizes if manifest sizes are empty (shouldn't happen)
                    var sizesToGenerate = manifestSizes.Count > 0 ? manifestSizes : sizes;

                    // Calculate total variants for this image
                    var variantsTotal = sizesToGenerate.Count * formats.Count;
                    var variantsDone = 0;
                    var variantsSkipped = 0;

                    // Update worker state: starting new image
                    workerStates[workerId] = new WorkerState
                    {
                        WorkerId = workerId,
                        ImageName = Path.GetFileName(sourcePath),
                        VariantsTotal = variantsTotal,
                        VariantsDone = 0,
                        VariantsSkipped = 0
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
                            OutputDirectory = outputImagesDirectory,
                            CacheDirectory = cacheDirectory
                        },
                        onVariantSaved: skipped =>
                        {
                            // Update variant progress
                            if (skipped)
                            {
                                Interlocked.Increment(ref variantsSkipped);
                            }
                            else
                            {
                                Interlocked.Increment(ref variantsDone);
                            }

                            // Update worker state
                            workerStates[workerId] = new WorkerState
                            {
                                WorkerId = workerId,
                                ImageName = Path.GetFileName(sourcePath),
                                VariantsTotal = variantsTotal,
                                VariantsDone = variantsDone,
                                VariantsSkipped = variantsSkipped
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

                    // Count files created: sizes × formats
                    var filesCreated = image.Sizes.Count * formats.Count;

                    // Thread-safe updates
                    var currentProcessed = Interlocked.Increment(ref processedCount);
                    Interlocked.Add(ref totalFilesCreated, filesCreated);

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
                var sizeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var fileCount = 0;

                foreach (var file in Directory.EnumerateFiles(outputImagesDirectory, "*.*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    fileCount++;
                    totalSizeBytes += new FileInfo(file).Length;
                }

                sizeStopwatch.Stop();
                LogOutputSizeScanned(logger, fileCount, totalSizeBytes, sizeStopwatch.Elapsed);
            }

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

    #region Output Validation

    /// <summary>
    /// Check if at least one output file exists for the image.
    /// </summary>
    /// <remarks>
    /// Only checks if the first size/format combination exists.
    /// If that exists, we assume all outputs are present.
    /// This is a quick check to detect missing outputs (e.g., deleted output folder).
    /// </remarks>
    private static bool OutputFilesExist(
        string outputDirectory,
        string imageName,
        IReadOnlyList<int> sizes,
        IReadOnlyDictionary<string, int> formats)
    {
        if (string.IsNullOrEmpty(imageName) || sizes.Count == 0 || formats.Count == 0)
        {
            return false;
        }

        // Check first size/format combination
        // Output path pattern: images/{imageName}/{width}.{format}
        var firstSize = sizes[0];
        var firstFormat = formats.Keys.First();
        var expectedPath = Path.Combine(outputDirectory, imageName, $"{firstSize}.{firstFormat}");

        return File.Exists(expectedPath);
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Config changed, rebuilding all images")]
    private static partial void LogConfigChanged(ILogger logger);

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing failed")]
    private static partial void LogImageProcessingFailed(ILogger logger, Exception exception);

    #endregion
}
