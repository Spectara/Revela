using System.Globalization;
using System.Text.RegularExpressions;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Provides natural sorting for gallery items using .NET 10 NumericOrdering
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="CompareOptions.NumericOrdering"/> (new in .NET 10) to sort
/// strings containing numbers in natural order (1, 2, 10 instead of 1, 10, 2).
/// </para>
/// <para>
/// Supports extracting display names from sort-prefixed folder names:
/// </para>
/// <list type="bullet">
///   <item><description>"01 Events" → "Events"</description></item>
///   <item><description>"2024 Summer" → "2024 Summer" (year-prefixed, kept as-is)</description></item>
///   <item><description>"Events" → "Events" (no prefix)</description></item>
/// </list>
/// </remarks>
public static partial class GallerySorter
{
    /// <summary>
    /// Regex to extract display name from sort-prefixed folder names
    /// </summary>
    /// <remarks>
    /// Matches: "01 Events", "99 Test", "1 Foo"
    /// Does not match: "2024 Summer" (year-prefixed names are kept as-is)
    /// </remarks>
    [GeneratedRegex(@"^(\d{1,2})\s+(.+)$", RegexOptions.Compiled)]
    private static partial Regex SortPrefixRegex();

    /// <summary>
    /// Comparer for natural string sorting (1, 2, 10 instead of 1, 10, 2)
    /// </summary>
    /// <remarks>
    /// Uses .NET 10's NumericOrdering for culture-aware natural sorting.
    /// InvariantCulture ensures consistent ordering across all systems.
    /// </remarks>
    public static StringComparer NaturalComparer { get; } =
        StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering);

    /// <summary>
    /// Extracts display name from a folder name, removing sort prefix if present
    /// </summary>
    /// <param name="folderName">Original folder name (e.g., "01 Events")</param>
    /// <returns>Display name without sort prefix (e.g., "Events")</returns>
    /// <example>
    /// <code>
    /// ExtractDisplayName("01 Events")     // → "Events"
    /// ExtractDisplayName("99 Test")       // → "Test"
    /// ExtractDisplayName("2024 Summer")   // → "2024 Summer" (year, not stripped)
    /// ExtractDisplayName("Events")        // → "Events" (no prefix)
    /// </code>
    /// </example>
    public static string ExtractDisplayName(string folderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        var match = SortPrefixRegex().Match(folderName);
        return match.Success ? match.Groups[2].Value : folderName;
    }

    /// <summary>
    /// Sorts items using natural ordering (1, 2, 10 instead of 1, 10, 2)
    /// </summary>
    /// <typeparam name="T">Type of items to sort</typeparam>
    /// <param name="items">Items to sort</param>
    /// <param name="keySelector">Function to extract sort key from item</param>
    /// <param name="descending">Whether to sort in descending order</param>
    /// <returns>Sorted items</returns>
    public static IOrderedEnumerable<T> SortNatural<T>(
        this IEnumerable<T> items,
        Func<T, string> keySelector,
        bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keySelector);

        return descending
            ? items.OrderByDescending(keySelector, NaturalComparer)
            : items.OrderBy(keySelector, NaturalComparer);
    }

    /// <summary>
    /// Sorts file paths using natural ordering
    /// </summary>
    /// <param name="paths">File or directory paths to sort</param>
    /// <param name="descending">Whether to sort in descending order</param>
    /// <returns>Sorted paths</returns>
    public static IOrderedEnumerable<string> SortPathsNatural(
        this IEnumerable<string> paths,
        bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return paths.SortNatural(Path.GetFileName, descending);
    }

    /// <summary>
    /// Sorts DirectoryInfo instances using natural ordering
    /// </summary>
    /// <param name="directories">Directories to sort</param>
    /// <param name="descending">Whether to sort in descending order</param>
    /// <returns>Sorted directories</returns>
    public static IOrderedEnumerable<DirectoryInfo> SortNatural(
        this IEnumerable<DirectoryInfo> directories,
        bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(directories);

        return directories.SortNatural(d => d.Name, descending);
    }

    /// <summary>
    /// Sorts FileInfo instances using natural ordering
    /// </summary>
    /// <param name="files">Files to sort</param>
    /// <param name="descending">Whether to sort in descending order</param>
    /// <returns>Sorted files</returns>
    public static IOrderedEnumerable<FileInfo> SortNatural(
        this IEnumerable<FileInfo> files,
        bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(files);

        return files.SortNatural(f => f.Name, descending);
    }
}
