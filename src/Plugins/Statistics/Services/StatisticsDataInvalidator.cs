using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Statistics.Services;

internal sealed class StatisticsDataInvalidator(
    IOptions<ProjectEnvironment> projectEnvironment) : IArtifactInvalidator
{
    internal const string FileName = "statistics.json";

    public ArtifactId Artifact => StatisticsArtifacts.Data;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.Manifest];

    public ValueTask<OperationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        var cachePath = Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache);
        var deletion = DerivedFiles.DeleteAll(cachePath, FileName, cancellationToken);

        return new ValueTask<OperationResult>(deletion.Failures.Count == 0
            ? OperationResult.Ok()
            : OperationResult.Fail(
                $"Could not delete '{deletion.Failures[0].Path}': {deletion.Failures[0].Message}"));
    }
}
