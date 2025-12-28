using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Commands.Generate.Pipeline;

/// <summary>
/// Pipeline step for HTML page generation.
/// </summary>
public sealed partial class PagesPipelineStep(
    ILogger<PagesPipelineStep> logger,
    IRenderService renderService,
    IManifestRepository manifestRepository) : IGeneratePipelineStep
{
    /// <inheritdoc />
    public string Name => "pages";

    /// <inheritdoc />
    public string Description => "Generate HTML pages from manifest";

    /// <inheritdoc />
    public int Order => PipelineOrder.Pages;

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

            var serviceProgress = new Progress<RenderProgress>(p =>
                progress?.Report(new PipelineProgress(p.Rendered, p.Total, p.CurrentPage)));

            var result = await renderService.RenderAsync(serviceProgress, cancellationToken);

            if (result.Success)
            {
                LogPagesComplete(logger, result.PageCount);
                return PipelineStepResult.Ok(
                    $"{result.PageCount} pages",
                    result.PageCount);
            }

            LogPagesFailed(logger, result.ErrorMessage ?? "Unknown error");
            return PipelineStepResult.Fail(result.ErrorMessage ?? "Page generation failed");
        }
        catch (OperationCanceledException)
        {
            return PipelineStepResult.Fail("Cancelled");
        }
        catch (Exception ex)
        {
            LogPagesException(logger, ex);
            return PipelineStepResult.Fail(ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pages complete: {Count} pages")]
    private static partial void LogPagesComplete(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pages failed: {Error}")]
    private static partial void LogPagesFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pages exception")]
    private static partial void LogPagesException(ILogger logger, Exception exception);
}
