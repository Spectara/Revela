using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Commands.Generate.Pipeline;

/// <summary>
/// Pipeline step for content scanning.
/// </summary>
public sealed partial class ScanPipelineStep(
    ILogger<ScanPipelineStep> logger,
    IContentService contentService) : IGeneratePipelineStep
{
    /// <inheritdoc />
    public string Name => "scan";

    /// <inheritdoc />
    public string Description => "Scan content and update manifest";

    /// <inheritdoc />
    public int Order => PipelineOrder.Scan;

    /// <inheritdoc />
    public async Task<PipelineStepResult> ExecuteAsync(
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serviceProgress = new Progress<ContentProgress>(p =>
                progress?.Report(new PipelineProgress(
                    p.GalleriesFound + p.ImagesFound,
                    0, // Total unknown during scan
                    p.Status)));

            var result = await contentService.ScanAsync(serviceProgress, cancellationToken);

            if (result.Success)
            {
                LogScanComplete(logger, result.GalleryCount, result.ImageCount);
                return PipelineStepResult.Ok(
                    $"{result.GalleryCount} galleries, {result.ImageCount} images",
                    result.ImageCount);
            }

            LogScanFailed(logger, result.ErrorMessage ?? "Unknown error");
            return PipelineStepResult.Fail(result.ErrorMessage ?? "Scan failed");
        }
        catch (OperationCanceledException)
        {
            return PipelineStepResult.Fail("Cancelled");
        }
        catch (Exception ex)
        {
            LogScanException(logger, ex);
            return PipelineStepResult.Fail(ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Scan complete: {Galleries} galleries, {Images} images")]
    private static partial void LogScanComplete(ILogger logger, int galleries, int images);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan failed: {Error}")]
    private static partial void LogScanFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan exception")]
    private static partial void LogScanException(ILogger logger, Exception exception);
}
