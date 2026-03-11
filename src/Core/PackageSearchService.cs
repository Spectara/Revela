using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Core;

/// <summary>
/// Searches NuGet package sources for Revela plugins and themes.
/// </summary>
/// <remarks>
/// Supports multi-source discovery — tries all configured NuGet feeds.
/// Package types are inferred from naming convention when NuGet API doesn't provide them.
/// </remarks>
public sealed class PackageSearchService(
    INuGetSourceManager nugetSourceManager,
    ILogger<PackageSearchService> logger)
{
    /// <summary>
    /// Searches all configured NuGet sources for packages matching the search term.
    /// </summary>
    /// <param name="searchTerm">Search term (e.g., "Spectara.Revela.Theme").</param>
    /// <param name="packageTypeFilter">Filter by package type (e.g., "RevelaTheme", "RevelaPlugin").</param>
    /// <param name="includePrerelease">Include prerelease versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching packages from all sources.</returns>
    public async Task<IReadOnlyList<PackageSearchResult>> SearchAsync(
        string searchTerm,
        string? packageTypeFilter = null,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        List<PackageSearchResult> results = [];
        var sources = await nugetSourceManager.LoadSourcesAsync(cancellationToken);

        logger.SearchingPackages(searchTerm, sources.Count);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var sourceRepo = Repository.Factory.GetCoreV3(new PackageSource(source.Url));
                var searchResource = await sourceRepo.GetResourceAsync<PackageSearchResource>(cancellationToken);

                if (searchResource is null)
                {
                    logger.SearchResourceNotAvailable(source.Name);
                    continue;
                }

                var searchFilter = new SearchFilter(includePrerelease);
                var packages = await searchResource.SearchAsync(
                    searchTerm,
                    searchFilter,
                    skip: 0,
                    take: 50,
                    NuGet.Common.NullLogger.Instance,
                    cancellationToken);

                foreach (var package in packages)
                {
                    // Skip if already added from another source (prefer first source)
                    if (results.Any(r => r.Id.Equals(package.Identity.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Infer package types from naming convention
                    var packageTypes = InferPackageTypes(package.Identity.Id);

                    // Apply package type filter if specified
                    if (!string.IsNullOrEmpty(packageTypeFilter))
                    {
                        if (!packageTypes.Contains(packageTypeFilter, StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    results.Add(new PackageSearchResult
                    {
                        Id = package.Identity.Id,
                        Version = package.Identity.Version.ToString(),
                        Description = package.Description,
                        Authors = package.Authors?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                        PackageTypes = packageTypes,
                        SourceName = source.Name,
                        DownloadCount = package.DownloadCount
                    });
                }

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.SearchSourceCompleted(source.Name, packages.Count());
                }
            }
            catch (Exception ex)
            {
                logger.SearchSourceFailed(source.Name, ex);
            }
        }

        logger.SearchCompleted(searchTerm, results.Count);
        return results;
    }

    /// <summary>
    /// Infers package types from naming convention.
    /// </summary>
    /// <remarks>
    /// Used for search results where NuGet API doesn't return PackageTypes.
    /// Real types are read from .nuspec during installation.
    /// Naming convention:
    /// - Spectara.Revela.Themes.* → RevelaTheme
    /// - Spectara.Revela.Plugins.* → RevelaPlugin
    /// </remarks>
    internal static List<string> InferPackageTypes(string packageId)
    {
        List<string> types = [];

        if (packageId.Contains(".Theme.", StringComparison.OrdinalIgnoreCase))
        {
            types.Add("RevelaTheme");
        }

        if (packageId.Contains(".Plugin.", StringComparison.OrdinalIgnoreCase))
        {
            types.Add("RevelaPlugin");
        }

        return types;
    }
}
