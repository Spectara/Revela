namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for PackageSearchService using source-generated extension methods.
/// </summary>
internal static partial class PackageSearchServiceLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Searching for packages matching '{SearchTerm}' in {SourceCount} source(s)")]
    public static partial void SearchingPackages(this ILogger<PackageSearchService> logger, string searchTerm, int sourceCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search resource not available for source '{SourceName}'")]
    public static partial void SearchResourceNotAvailable(this ILogger<PackageSearchService> logger, string sourceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Search completed for source '{SourceName}': {PackageCount} package(s) found")]
    public static partial void SearchSourceCompleted(this ILogger<PackageSearchService> logger, string sourceName, int packageCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search failed for source '{SourceName}'")]
    public static partial void SearchSourceFailed(this ILogger<PackageSearchService> logger, string sourceName, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Search for '{SearchTerm}' completed: {TotalCount} package(s) found")]
    public static partial void SearchCompleted(this ILogger<PackageSearchService> logger, string searchTerm, int totalCount);
}
