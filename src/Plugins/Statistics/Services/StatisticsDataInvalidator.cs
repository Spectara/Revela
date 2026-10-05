using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Statistics.Services;

/// <summary>
/// Removes generated statistics data files from the plugin's folder <c>.revela/statistics</c>.
/// </summary>
/// <remarks>
/// A page may name its data file (<c>data.statistics</c>), so every <c>*.json</c> file below the
/// plugin's folder is statistics data: the folder belongs to this plugin, and the data is its
/// only artifact there.
/// </remarks>
internal sealed class StatisticsDataInvalidator(
    IOptions<ProjectEnvironment> projectEnvironment) : IArtifactInvalidator
{
    /// <summary>Data file name used when a page does not set <c>data.statistics</c>.</summary>
    internal const string DefaultFileName = "statistics.json";

    private const string DataFilePattern = "*.json";

    public ArtifactId Artifact => StatisticsArtifacts.Data;

    public ArtifactKind Kind => ArtifactKind.Cache;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.Manifest];

    public ValueTask<OperationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        var deletion = DerivedFiles.DeleteAll(GetDataDirectory(projectEnvironment.Value.Path), DataFilePattern, cancellationToken);

        return new ValueTask<OperationResult>(deletion.Failures.Count == 0
            ? OperationResult.Ok()
            : OperationResult.Fail(
                $"Could not delete '{deletion.Failures[0].Path}': {deletion.Failures[0].Message}"));
    }

    /// <summary>Gets the plugin's folder, <c>.revela/statistics</c>, which holds one folder per page.</summary>
    internal static string GetDataDirectory(string projectPath) =>
        Path.Combine(projectPath, ProjectPaths.GetOwnerDirectory(StatisticsArtifacts.Data.Owner));
}
