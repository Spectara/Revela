using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Plugins.Compress.Services;

internal sealed class CompressedSiteInvalidator(IPathResolver pathResolver) : IArtifactInvalidator
{
    public ArtifactId Artifact => CompressArtifacts.PrecompressedSite;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.RenderedSite];

    public async ValueTask<OperationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outputPath = pathResolver.OutputPath;
        if (!Directory.Exists(outputPath))
        {
            return OperationResult.Ok();
        }

        try
        {
            using var ownership = await CompressedSiteOwnership.OpenAsync(outputPath, cancellationToken);
            await ownership.CleanAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperationResult.Fail(
                $"Could not clean compressed artifacts in '{outputPath}': {exception.Message}");
        }

        return OperationResult.Ok();
    }
}
