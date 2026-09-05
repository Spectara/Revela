using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Statistics.Services;

internal sealed class StatisticsDataInvalidator(
    IOptions<ProjectEnvironment> projectEnvironment) : IArtifactInvalidator
{
    private const string StatisticsFileName = "statistics.json";

    public ArtifactId Artifact => StatisticsArtifacts.Data;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.Manifest];

    public ValueTask<ArtifactInvalidationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        var cachePath = Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache);
        if (!Directory.Exists(cachePath))
        {
            return new ValueTask<ArtifactInvalidationResult>(ArtifactInvalidationResult.Ok());
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(
                         cachePath,
                         StatisticsFileName,
                         new EnumerationOptions
                         {
                             RecurseSubdirectories = true,
                             AttributesToSkip = FileAttributes.ReparsePoint
                         }))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ValueTask<ArtifactInvalidationResult>(ArtifactInvalidationResult.Fail(
                $"Could not clean statistics artifacts in '{cachePath}': {exception.Message}"));
        }

        return new ValueTask<ArtifactInvalidationResult>(ArtifactInvalidationResult.Ok());
    }
}
