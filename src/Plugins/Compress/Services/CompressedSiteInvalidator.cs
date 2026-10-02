using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Plugins.Compress.Services;

/// <summary>
/// Removes the pre-compressed sidecars and their ownership record
/// (<see cref="CompressArtifacts.PrecompressedSite"/>, an <see cref="ArtifactKind.Output"/> artifact).
/// </summary>
/// <remarks>
/// Only files listed in the record are deleted, and only while unchanged. Without an output
/// directory the record describes nothing and is deleted on its own.
/// </remarks>
internal sealed class CompressedSiteInvalidator(
    IPathResolver pathResolver,
    IOptions<ProjectEnvironment> projectEnvironment) : IArtifactInvalidator
{
    public ArtifactId Artifact => CompressArtifacts.PrecompressedSite;

    public ArtifactKind Kind => ArtifactKind.Output;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.RenderedSite];

    public async ValueTask<OperationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outputPath = pathResolver.OutputPath;
        var ownerDirectory = CompressedSiteOwnership.GetOwnerDirectory(projectEnvironment.Value.Path);

        try
        {
            if (!Directory.Exists(outputPath))
            {
                var record = Path.Combine(ownerDirectory, CompressedSiteOwnership.RecordFileName);
                if (File.Exists(record))
                {
                    File.Delete(record);
                }

                return OperationResult.Ok();
            }

            using var ownership = await CompressedSiteOwnership.OpenAsync(outputPath, ownerDirectory, cancellationToken);
            await ownership.CleanAsync(cancellationToken);
            await ownership.DeleteEmptyRecordAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperationResult.Fail(
                $"Could not clean compressed artifacts in '{outputPath}': {exception.Message}");
        }

        return OperationResult.Ok();
    }
}
