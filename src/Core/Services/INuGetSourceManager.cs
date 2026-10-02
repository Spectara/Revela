using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Interface for managing NuGet package sources configuration
/// </summary>
public interface INuGetSourceManager
{
    /// <summary>
    /// Loads all usable sources (bundled, nuget.org, global feeds and approved project feeds)
    /// </summary>
    /// <remarks>
    /// Relative paths are resolved relative to the file that declares the feed.
    /// Feeds declared only in project.json are excluded until <see cref="ApproveProjectFeeds"/> is called.
    /// </remarks>
    Task<List<NuGetSource>> LoadSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all usable sources with location info for display
    /// </summary>
    /// <remarks>
    /// Returns tuples of (Source, Location) where Location is "bundled", "built-in", "remote", or "local".
    /// Unapproved project feeds are excluded (see <see cref="GetProjectFeeds"/>).
    /// </remarks>
    Task<List<(NuGetSource Source, string Location)>> GetAllSourcesWithLocationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path of the project.json whose feeds are inspected, or <c>null</c> outside a project.
    /// </summary>
    string? ProjectConfigPath { get; }

    /// <summary>
    /// Gets feeds declared in project.json that are not also declared in revela.json (approved or not).
    /// </summary>
    IReadOnlyList<NuGetSource> GetProjectFeeds();

    /// <summary>
    /// Gets project feeds that have not been approved for this process yet.
    /// </summary>
    IReadOnlyList<NuGetSource> GetPendingProjectFeeds();

    /// <summary>
    /// Allows project feeds to be used as package sources for the rest of this process.
    /// </summary>
    void ApproveProjectFeeds();
}
