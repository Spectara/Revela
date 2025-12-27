using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Service for loading and searching the local package index.
/// </summary>
/// <remarks>
/// The package index is cached at cache/packages.json and must be
/// refreshed using 'revela packages refresh' before use.
/// </remarks>
public interface IPackageIndexService
{
    /// <summary>
    /// Loads the package index from cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The package index, or null if not found.</returns>
    Task<PackageIndex?> LoadIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a package by ID in the index.
    /// </summary>
    /// <param name="packageId">Package ID to find.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The package entry, or null if not found.</returns>
    Task<PackageIndexEntry?> FindPackageAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches packages by type.
    /// </summary>
    /// <param name="packageType">Package type (e.g., "RevelaTheme", "RevelaPlugin").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching packages.</returns>
    Task<IReadOnlyList<PackageIndexEntry>> SearchByTypeAsync(
        string packageType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the age of the package index.
    /// </summary>
    /// <returns>Age of the index, or null if not found.</returns>
    TimeSpan? GetIndexAge();

    /// <summary>
    /// Gets the path to the package index file.
    /// </summary>
    string IndexFilePath { get; }
}
