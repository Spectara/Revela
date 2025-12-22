using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Interface for managing NuGet package sources configuration
/// </summary>
public interface INuGetSourceManager
{
    /// <summary>
    /// Loads all configured sources (including built-in nuget.org)
    /// </summary>
    /// <remarks>
    /// Relative paths in feed URLs are resolved relative to the config file location.
    /// </remarks>
    Task<List<NuGetSource>> LoadSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sources with location info for display
    /// </summary>
    /// <remarks>
    /// Returns tuples of (Source, Location) where Location is "built-in", "remote", or "local".
    /// Relative paths are resolved relative to the config file location.
    /// </remarks>
    Task<List<(NuGetSource Source, string Location)>> GetAllSourcesWithLocationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sources including built-in
    /// </summary>
    Task<List<NuGetSource>> GetAllSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new NuGet source
    /// </summary>
    /// <remarks>
    /// The URL is stored as-is (relative paths remain relative for portability).
    /// </remarks>
#pragma warning disable CA1054 // URI parameters should not be strings - string required for user input
    Task AddSourceAsync(string name, string url, CancellationToken cancellationToken = default);
#pragma warning restore CA1054

    /// <summary>
    /// Removes a NuGet source
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when attempting to remove built-in source 'nuget.org'</exception>
    Task<bool> RemoveSourceAsync(string name, CancellationToken cancellationToken = default);
}
