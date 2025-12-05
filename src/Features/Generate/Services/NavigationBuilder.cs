using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Builds hierarchical navigation from source directory structure
/// </summary>
/// <remarks>
/// <para>
/// Scans directories recursively and creates a navigation tree:
/// </para>
/// <list type="bullet">
///   <item><description>Directories with images → galleries (leaf nodes with URL)</description></item>
///   <item><description>Directories without images → sections (branch/column nodes)</description></item>
///   <item><description>Sort prefixes stripped from display names</description></item>
///   <item><description>Natural sorting (1, 2, 10 not 1, 10, 2)</description></item>
///   <item><description>_index.md frontmatter for custom titles, slugs, descriptions</description></item>
/// </list>
/// <para>
/// Example directory structure:
/// </para>
/// <code>
/// content/
/// ├── 01 Events/
/// │   ├── _index.md            → Metadata: title, description, etc.
/// │   ├── 2024 Wedding/
/// │   │   └── *.jpg           → Gallery: "2024 Wedding"
/// │   └── 2023 Party/
/// │       └── *.jpg           → Gallery: "2023 Party"
/// └── 02 Portraits/
///     └── *.jpg               → Gallery: "Portraits"
/// </code>
/// </remarks>
public sealed partial class NavigationBuilder(ILogger<NavigationBuilder> logger)
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    /// <summary>
    /// Builds navigation tree from source directory
    /// </summary>
    /// <param name="sourceDirectory">Root content directory</param>
    /// <param name="currentPath">Current page path for active state (e.g., "events/2024/")</param>
    /// <param name="sortDescending">Sort folders in descending order</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of top-level navigation items</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1054:URI-like parameters should not be strings",
        Justification = "currentPath is a relative path segment, not a full URI")]
    public Task<IReadOnlyList<NavigationItem>> BuildAsync(
        string sourceDirectory,
        string? currentPath = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken; // Reserved for future async operations

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);

        LogBuildingNavigation(logger, sourceDirectory);

        var items = BuildNavigationRecursive(
            new DirectoryInfo(sourceDirectory),
            pathSegments: [],
            currentPath ?? string.Empty,
            sortDescending);

        LogNavigationComplete(logger, CountItems(items));

        return Task.FromResult<IReadOnlyList<NavigationItem>>(items);
    }

    /// <summary>
    /// Recursively builds navigation from directory structure
    /// </summary>
    private List<NavigationItem> BuildNavigationRecursive(
        DirectoryInfo directory,
        List<string> pathSegments,
        string currentPath,
        bool sortDescending)
    {
        if (!directory.Exists)
        {
            return [];
        }

        var items = new List<NavigationItem>();

        // Get and sort subdirectories
        var subdirectories = directory
            .EnumerateDirectories()
            .Where(d => !d.Name.StartsWith('.'))  // Skip hidden dirs
            .SortNatural(sortDescending)
            .ToList();

        foreach (var subdir in subdirectories)
        {
            // Load metadata from _index.md if present
            var indexPath = Path.Combine(subdir.FullName, FrontMatterParser.IndexFileName);
            var metadata = File.Exists(indexPath)
                ? FrontMatterParser.Parse(File.ReadAllText(indexPath))
                : DirectoryMetadata.Empty;

            // Use metadata title or extract from folder name
            var displayName = metadata.Title ?? GallerySorter.ExtractDisplayName(subdir.Name);

            // Use metadata slug or folder name for path
            var pathSegment = metadata.Slug ?? subdir.Name;
            var newPathSegments = new List<string>(pathSegments) { pathSegment };
            var url = UrlBuilder.BuildPath([.. newPathSegments]);

            // Check if directory contains images
            var hasImages = HasImages(subdir);

            // Recursively get children
            var children = BuildNavigationRecursive(
                subdir,
                newPathSegments,
                currentPath,
                sortDescending);

            // Determine if this item is active
            var isActive = !string.IsNullOrEmpty(currentPath) &&
                          (currentPath.Equals(url, StringComparison.OrdinalIgnoreCase) ||
                           currentPath.StartsWith(url, StringComparison.OrdinalIgnoreCase));

            // Create navigation item
            // - Has images → has URL (it's a gallery)
            // - No images + has children → section header (no URL)
            // - No images + no children → skip (empty folder)

            if (!hasImages && children.Count == 0)
            {
                LogSkippingEmptyDirectory(logger, subdir.FullName);
                continue;
            }

            items.Add(new NavigationItem
            {
                Text = displayName,
                Url = hasImages ? url : null,  // Only galleries have URLs
                Description = metadata.Description,
                Active = isActive,
                Hidden = metadata.Hidden,
                Children = children
            });
        }

        return items;
    }

    /// <summary>
    /// Checks if a directory contains image files
    /// </summary>
    private static bool HasImages(DirectoryInfo directory)
    {
        return directory
            .EnumerateFiles()
            .Any(f => ImageExtensions.Contains(
                f.Extension,
                StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Counts total items in navigation tree (recursive)
    /// </summary>
    private static int CountItems(IReadOnlyList<NavigationItem> items)
    {
        var count = items.Count;
        foreach (var item in items)
        {
            count += CountItems(item.Children);
        }

        return count;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building navigation from: {SourceDirectory}")]
    private static partial void LogBuildingNavigation(ILogger logger, string sourceDirectory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping empty directory: {Directory}")]
    private static partial void LogSkippingEmptyDirectory(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Navigation complete: {Count} items")]
    private static partial void LogNavigationComplete(ILogger logger, int count);
}
