using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Calendar.Services;

/// <summary>
/// Removes generated <c>calendar.json</c> files from the project cache.
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

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.Manifest];

    public ValueTask<ArtifactInvalidationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        var cachePath = Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache);
        var deletion = DerivedFiles.DeleteAll(cachePath, FileName, cancellationToken);

        return new ValueTask<ArtifactInvalidationResult>(deletion.Failures.Count == 0
            ? ArtifactInvalidationResult.Ok()
            : ArtifactInvalidationResult.Fail(
                $"Could not delete '{deletion.Failures[0].Path}': {deletion.Failures[0].Message}"));
    }
}
