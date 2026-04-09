using System.Diagnostics;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Abstractions.Engine;
using Spectara.Revela.Sdk.Models.Engine;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Default implementation of <see cref="IRevelaEngine"/>.
/// Delegates to the internal content, render, and image services.
/// </summary>
/// <remarks>
/// <para>
/// Individual methods (ScanAsync, GeneratePagesAsync, GenerateImagesAsync) use
/// the internal services directly and return rich result types.
/// </para>
/// <para>
/// <see cref="GenerateAllAsync"/> iterates ALL registered <see cref="IGenerateStep"/>
/// implementations in order — identical behavior to the CLI <c>generate all</c> command.
/// This ensures plugin steps (Statistics, Calendar) are included in the pipeline.
/// </para>
/// </remarks>
internal sealed partial class RevelaEngine(
    ILogger<RevelaEngine> logger,
    IContentService contentService,
    IRenderService renderService,
    IImageService imageService,
    IEnumerable<IGenerateStep> generateSteps) : IRevelaEngine
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
    /// <remarks>
    /// <para>
    /// Runs ALL registered <see cref="IGenerateStep"/> implementations in Order sequence,
    /// identical to the CLI <c>generate all</c> command. This ensures plugin steps
    /// (Statistics, Calendar, etc.) are included in the pipeline.
    /// </para>
    /// <para>
    /// Note: This method delegates to <see cref="IGenerateStep"/> implementations which
    /// currently write directly to the console via Spectre.Console. The individual result
    /// properties (Scan, Pages, Images) are not populated because plugin steps (Statistics,
    /// Calendar) don't map to those three phases. This is intentional — for rich per-phase
    /// results, use the individual methods (ScanAsync, GeneratePagesAsync, GenerateImagesAsync).
    /// </para>
    /// </remarks>
    public async Task<GenerateResult> GenerateAllAsync(
        IProgress<GenerateProgress>? progress,
        CancellationToken cancellationToken)
    {
        var steps = generateSteps.OrderBy(s => s.Order).ToList();

        if (steps.Count == 0)
        {
            return new GenerateResult
            {
                Success = false,
                Duration = TimeSpan.Zero,
                ErrorMessage = "No pipeline steps registered",
            };
        }

        LogGenerateAllStarted(logger, steps.Count);
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            progress?.Report(new GenerateProgress
            {
                StepName = step.Name,
                CurrentStep = i,
                TotalSteps = steps.Count,
            });

            var exitCode = await step.ExecuteAsync(cancellationToken);
            if (exitCode != 0)
            {
                LogStepFailed(logger, step.Name, exitCode);
                return new GenerateResult
                {
                    Success = false,
                    Duration = stopwatch.Elapsed,
                    ErrorMessage = $"Step '{step.Name}' failed with exit code {exitCode}",
                };
            }
        }

        stopwatch.Stop();
        LogGenerateAllCompleted(logger, stopwatch.Elapsed);

        return new GenerateResult
        {
            Success = true,
            Duration = stopwatch.Elapsed,
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Starting full generation pipeline with {StepCount} step(s)")]
    private static partial void LogGenerateAllStarted(ILogger logger, int stepCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Engine: Pipeline step '{StepName}' failed with exit code {ExitCode}")]
    private static partial void LogStepFailed(ILogger logger, string stepName, int exitCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine: Full pipeline completed in {Duration}")]
    private static partial void LogGenerateAllCompleted(ILogger logger, TimeSpan duration);
}

