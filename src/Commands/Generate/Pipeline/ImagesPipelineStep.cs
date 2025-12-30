using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Commands.Generate.Pipeline;

/// <summary>
/// Pipeline step for image processing.
/// </summary>
public sealed partial class ImagesPipelineStep(
    ILogger<ImagesPipelineStep> logger,
    IImageService imageService,
    IManifestRepository manifestRepository) : IGeneratePipelineStep
{
    /// <inheritdoc />
    public string Name => "images";

    /// <inheritdoc />
    public string Description => "Process images from manifest";

    /// <inheritdoc />
    public int Order => PipelineOrder.Images;

    /// <inheritdoc />
    public async Task<PipelineStepResult> ExecuteAsync(
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check manifest exists
            await manifestRepository.LoadAsync(cancellationToken);
            if (manifestRepository.Root is null)
            {
                return PipelineStepResult.Fail("No manifest found. Run scan first.");
            }

            var options = new ProcessImagesOptions { Force = false };

            var serviceProgress = new Progress<ImageProgress>(p =>
            {
                var currentImage = p.Workers.Count > 0 ? p.Workers[0].ImageName ?? "" : "";
                progress?.Report(new PipelineProgress(p.Processed, p.Total, currentImage));
            });

            var result = await imageService.ProcessAsync(options, serviceProgress, cancellationToken);

            if (result.Success)
            {
                LogImagesComplete(logger, result.ProcessedCount, result.SkippedCount);
                return PipelineStepResult.Ok(
                    $"{result.ProcessedCount} processed, {result.SkippedCount} cached",
                    result.ProcessedCount);
            }

            LogImagesFailed(logger, result.ErrorMessage ?? "Unknown error");
            return PipelineStepResult.Fail(result.ErrorMessage ?? "Image processing failed");
        }
        catch (OperationCanceledException)
        {
            return PipelineStepResult.Fail("Cancelled");
        }
        catch (Exception ex)
        {
            LogImagesException(logger, ex);
            return PipelineStepResult.Fail(ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Images complete: {Processed} processed, {Skipped} cached")]
    private static partial void LogImagesComplete(ILogger logger, int processed, int skipped);

    [LoggerMessage(Level = LogLevel.Error, Message = "Images failed: {Error}")]
    private static partial void LogImagesFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Images exception")]
    private static partial void LogImagesException(ILogger logger, Exception exception);
}
