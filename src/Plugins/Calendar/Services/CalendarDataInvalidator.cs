using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Calendar.Services;

/// <summary>
/// Removes generated <c>calendar.json</c> files from the plugin's folder <c>.revela/calendar</c>.
/// </summary>
/// <remarks>
/// Calendar data is derived from the manifest (which pages are calendar pages), so it is
/// invalidated whenever the manifest is replaced and before new calendar data is written.
/// </remarks>
internal sealed class CalendarDataInvalidator(
    IOptions<ProjectEnvironment> projectEnvironment) : IArtifactInvalidator
{
    internal const string FileName = "calendar.json";

    public ArtifactId Artifact => CalendarArtifacts.Data;

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

    /// <summary>Gets the plugin's folder, <c>.revela/calendar</c>, which holds one folder per page.</summary>
    internal static string GetDataDirectory(string projectPath) =>
        Path.Combine(projectPath, ProjectPaths.GetOwnerDirectory(CalendarArtifacts.Data.Owner));
}
