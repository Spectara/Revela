using System.Text.Json;

using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Service for loading and searching the local package index.
/// </summary>
public sealed class PackageIndexService : IPackageIndexService
{
    private readonly TimeProvider timeProvider;
    private PackageIndex? cachedIndex;
    private DateTime? lastLoadTime;

    /// <summary>
    /// Creates a service that reads the index from the global config directory.
    /// </summary>
    public PackageIndexService(TimeProvider timeProvider)
        : this(timeProvider, Path.Combine(ConfigPathResolver.ConfigDirectory, "packages.json"))
    {
    }

    internal PackageIndexService(TimeProvider timeProvider, string indexFilePath)
    {
        this.timeProvider = timeProvider;
        IndexFilePath = indexFilePath;
    }

    /// <inheritdoc />
    public string IndexFilePath { get; }

    /// <inheritdoc />
    public async Task<PackageIndex?> LoadIndexAsync(CancellationToken cancellationToken = default)
    {
        // Return cached if loaded within last minute
        if (cachedIndex is not null && lastLoadTime.HasValue &&
            timeProvider.GetUtcNow().UtcDateTime - lastLoadTime.Value < TimeSpan.FromMinutes(1))
        {
            return cachedIndex;
        }

        if (!File.Exists(IndexFilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(IndexFilePath, cancellationToken);
            cachedIndex = JsonSerializer.Deserialize(json, PackageIndexJsonContext.Default.PackageIndex);
            lastLoadTime = timeProvider.GetUtcNow().UtcDateTime;
            return cachedIndex;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PackageIndexEntry?> FindPackageAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        var index = await LoadIndexAsync(cancellationToken);
        if (index is null)
        {
            return null;
        }

        return index.Packages.FirstOrDefault(p =>
            PackageTrustPolicy.IsOfficialPackageId(p.Id) &&
            p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageIndexEntry>> SearchByTypeAsync(
        string packageType,
        CancellationToken cancellationToken = default)
    {
        var index = await LoadIndexAsync(cancellationToken);
        if (index is null)
        {
            return [];
        }

        return [.. index.Packages.Where(p =>
            PackageTrustPolicy.IsOfficialPackageId(p.Id) &&
            p.Types.Contains(packageType, StringComparer.OrdinalIgnoreCase))];
    }

    /// <inheritdoc />
    public TimeSpan? GetIndexAge()
    {
        if (!File.Exists(IndexFilePath))
        {
            return null;
        }

        try
        {
            if (cachedIndex is not null)
            {
                return timeProvider.GetUtcNow().UtcDateTime - cachedIndex.LastUpdated;
            }

            // Read just the lastUpdated field without full parsing
            var fileInfo = new FileInfo(IndexFilePath);
            return timeProvider.GetUtcNow().UtcDateTime - fileInfo.LastWriteTimeUtc;
        }
        catch
        {
            return null;
        }
    }
}
