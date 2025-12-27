using System.Text.Json;

using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Service for loading and searching the local package index.
/// </summary>
public sealed class PackageIndexService : IPackageIndexService
{
    private PackageIndex? cachedIndex;
    private DateTime? lastLoadTime;

    /// <inheritdoc />
    public string IndexFilePath { get; } = Path.Combine(
        ConfigPathResolver.ConfigDirectory, "cache", "packages.json");

    /// <inheritdoc />
    public async Task<PackageIndex?> LoadIndexAsync(CancellationToken cancellationToken = default)
    {
        // Return cached if loaded within last minute
        if (cachedIndex is not null && lastLoadTime.HasValue &&
            DateTime.UtcNow - lastLoadTime.Value < TimeSpan.FromMinutes(1))
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
            lastLoadTime = DateTime.UtcNow;
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

        return [.. index.Packages.Where(p => p.Types.Contains(packageType, StringComparer.OrdinalIgnoreCase))];
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
                return DateTime.UtcNow - cachedIndex.LastUpdated;
            }

            // Read just the lastUpdated field without full parsing
            var fileInfo = new FileInfo(IndexFilePath);
            return DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
        }
        catch
        {
            return null;
        }
    }
}
