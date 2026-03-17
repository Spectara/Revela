using Spectara.Revela.Plugins.Generate.Abstractions;
using Spectara.Revela.Plugins.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions.Engine;
using Spectara.Revela.Sdk.Models.Engine;

namespace Spectara.Revela.Plugins.Generate.Services;

/// <summary>
/// Default implementation of <see cref="IRevelaEngine"/>.
/// Delegates to the internal content, render, and image services.
/// </summary>
internal sealed partial class RevelaEngine(
    ILogger<RevelaEngine> logger,
    IContentService contentService,
    IRenderService renderService,
    IImageService imageService) : IRevelaEngine
{
    /// <inheritdoc />
    public async Task<ScanResult> ScanAsync(
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        LogScanStarted(logger);

        var internalProgress = progress is null
            ? null
            : new Progress<ContentProgress>(p => progress.Report(new ScanProgress
            {
                Status = p.Status,
                GalleriesFound = p.GalleriesFound,
                ImagesFound = p.ImagesFound,
            }));

        var result = await contentService.ScanAsync(internalProgress, cancellationToken);

        LogScanCompleted(logger, result.GalleryCount, result.ImageCount, result.Duration);

        return new ScanResult
        {
            Success = result.Success,
            GalleryCount = result.GalleryCount,
            ImageCount = result.ImageCount,
            NavigationItemCount = result.NavigationItemCount,
            Duration = result.Duration,
            ErrorMessage = result.ErrorMessage,
        };
    }

    /// <inheritdoc />
    public async Task<PagesResult> GeneratePagesAsync(
        IProgress<PagesProgress>? progress,
        CancellationToken cancellationToken)
    {
        LogPagesStarted(logger);

        var internalProgress = progress is null
            ? null
            : new Progress<RenderProgress>(p => progress.Report(new PagesProgress
            {
                CurrentPage = p.CurrentPage,
                Rendered = p.Rendered,
                Total = p.Total,
            }));

        var result = await renderService.RenderAsync(internalProgress, cancellationToken);

        LogPagesCompleted(logger, result.PageCount, result.Duration);

        return new PagesResult
        {
            Success = result.Success,
            PageCount = result.PageCount,
            Duration = result.Duration,
            ErrorMessage = result.ErrorMessage,
        };
    }

    /// <inheritdoc />
    public async Task<ImagesResult> GenerateImagesAsync(
        bool force,
        IProgress<ImagesProgress>? progress,
        CancellationToken cancellationToken)
    {
        LogImagesStarted(logger, force);

        var options = new ProcessImagesOptions { Force = force };
        var internalProgress = progress is null
            ? null
            : new Progress<ImageProgress>(p => progress.Report(new ImagesProgress
            {
                Processed = p.Processed,
                Total = p.Total,
                Skipped = p.Skipped,
            }));

        var result = await imageService.ProcessAsync(options, internalProgress, cancellationToken);

        LogImagesCompleted(logger, result.ProcessedCount, result.SkippedCount, result.Duration);

        return new ImagesResult
        {
            Success = result.Success,
            ProcessedCount = result.ProcessedCount,
            SkippedCount = result.SkippedCount,
            FilesCreated = result.FilesCreated,
            TotalSize = result.TotalSize,
            Duration = result.Duration,
            ErrorMessage = result.ErrorMessage,
            Warnings = result.Warnings,
        };
    }

    /// <inheritdoc />
    public async Task<GenerateResult> GenerateAllAsync(
        IProgress<GenerateProgress>? progress,
        CancellationToken cancellationToken)
    {
        LogGenerateAllStarted(logger);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Phase 1: Scan
        progress?.Report(new GenerateProgress { StepName = "Scan", CurrentStep = 0, TotalSteps = 3 });
        var scanResult = await ScanAsync(null, cancellationToken);
        if (!scanResult.Success)
        {
            return new GenerateResult
            {
                Success = false,
                Scan = scanResult,
                Duration = stopwatch.Elapsed,
                ErrorMessage = scanResult.ErrorMessage,
            };
        }

        // Phase 2: Pages
        progress?.Report(new GenerateProgress { StepName = "Pages", CurrentStep = 1, TotalSteps = 3 });
        var pagesResult = await GeneratePagesAsync(null, cancellationToken);
        if (!pagesResult.Success)
        {
            return new GenerateResult
            {
                Success = false,
                Scan = scanResult,
                Pages = pagesResult,
                Duration = stopwatch.Elapsed,
                ErrorMessage = pagesResult.ErrorMessage,
            };
        }

        // Phase 3: Images
        progress?.Report(new GenerateProgress { StepName = "Images", CurrentStep = 2, TotalSteps = 3 });
        var imagesResult = await GenerateImagesAsync(false, null, cancellationToken);

        stopwatch.Stop();
        LogGenerateAllCompleted(logger, stopwatch.Elapsed);

        return new GenerateResult
        {
            Success = imagesResult.Success,
            Scan = scanResult,
            Pages = pagesResult,
            Images = imagesResult,
            Duration = stopwatch.Elapsed,
            ErrorMessage = imagesResult.ErrorMessage,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Starting content scan")]
    private static partial void LogScanStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Scan completed — {GalleryCount} galleries, {ImageCount} images in {Duration}")]
    private static partial void LogScanCompleted(ILogger logger, int galleryCount, int imageCount, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Starting page rendering")]
    private static partial void LogPagesStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Pages completed — {PageCount} pages in {Duration}")]
    private static partial void LogPagesCompleted(ILogger logger, int pageCount, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Starting image processing (force={Force})")]
    private static partial void LogImagesStarted(ILogger logger, bool force);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Images completed — {ProcessedCount} processed, {SkippedCount} skipped in {Duration}")]
    private static partial void LogImagesCompleted(ILogger logger, int processedCount, int skippedCount, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Starting full generation pipeline")]
    private static partial void LogGenerateAllStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Full pipeline completed in {Duration}")]
    private static partial void LogGenerateAllCompleted(ILogger logger, TimeSpan duration);
}
