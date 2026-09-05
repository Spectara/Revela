using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Plugins.Compress.Services;

internal sealed class CompressedSiteInvalidator(IPathResolver pathResolver) : IArtifactInvalidator
{
    public ArtifactId Artifact => CompressArtifacts.PrecompressedSite;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.RenderedSite];

    public ValueTask<ArtifactInvalidationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        var outputPath = pathResolver.OutputPath;
        if (!Directory.Exists(outputPath))
        {
            return new ValueTask<ArtifactInvalidationResult>(ArtifactInvalidationResult.Ok());
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(
                         outputPath,
                         "*.*",
                         new EnumerationOptions
                         {
                             RecurseSubdirectories = true,
                             AttributesToSkip = FileAttributes.ReparsePoint
                         })
                         .Where(path => path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
                             path.EndsWith(".br", StringComparison.OrdinalIgnoreCase)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ValueTask<ArtifactInvalidationResult>(ArtifactInvalidationResult.Fail(
                $"Could not clean compressed artifacts in '{outputPath}': {exception.Message}"));
        }

        return new ValueTask<ArtifactInvalidationResult>(ArtifactInvalidationResult.Ok());
    }
}
