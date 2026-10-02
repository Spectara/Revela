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

    public ArtifactKind Kind => ArtifactKind.Cache;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.Manifest];

    public ValueTask<OperationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        var deletion = DerivedFiles.DeleteAll(GetDataDirectory(projectEnvironment.Value.Path), FileName, cancellationToken);

        return new ValueTask<OperationResult>(deletion.Failures.Count == 0
            ? OperationResult.Ok()
            : OperationResult.Fail(
                $"Could not delete '{deletion.Failures[0].Path}': {deletion.Failures[0].Message}"));
    }

    /// <summary>Gets the plugin's folder, <c>.revela/statistics</c>, which holds one folder per page.</summary>
    internal static string GetDataDirectory(string projectPath) =>
        Path.Combine(projectPath, ProjectPaths.GetOwnerDirectory(StatisticsArtifacts.Data.Owner));
}
