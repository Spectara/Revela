using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Spectara.Revela.Plugin.Source.OneDrive.Formatting;
using Spectara.Revela.Plugin.Source.OneDrive.Models;

namespace Spectara.Revela.Plugin.Source.OneDrive.Services;

/// <summary>
/// Analyzes local files vs OneDrive files to determine download actions
/// </summary>
public sealed partial class DownloadAnalyzer
{
    private const double LastModifiedToleranceSeconds = 5.0;

    private static readonly string[] DefaultExtensions = [".jpg", ".jpeg", ".png", ".webp", ".md"];

    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    /// <summary>
    /// Analyzes which files need to be downloaded
    /// </summary>
    /// <param name="remoteItems">Remote OneDrive items</param>
    /// <param name="destinationDirectory">Local destination directory</param>
    /// <param name="config">OneDrive configuration (for filter patterns)</param>
    /// <param name="includeOrphans">Whether to detect orphaned files</param>
    /// <param name="includeAllOrphans">Whether to include all orphans (not just filtered)</param>
    /// <param name="forceRefresh">Force re-download all files, even if they appear unchanged</param>
    /// <returns>Analysis result with items to download and orphaned files</returns>
#pragma warning disable CA1822 // Member can be static - kept as instance method for future extensibility
    public DownloadAnalysis Analyze(
        IReadOnlyList<OneDriveItem> remoteItems,
        string destinationDirectory,
        OneDriveConfig config,
        bool includeOrphans = false,
        bool includeAllOrphans = false,
        bool forceRefresh = false
    )
    {
        var items = new List<DownloadItem>();

        // Analyze remote items
        foreach (var remoteItem in remoteItems.Where(i => !i.IsFolder))
        {
            var relativePath = GetRelativePath(remoteItem);
            var localPath = Path.Combine(destinationDirectory, relativePath);
            var localFile = File.Exists(localPath) ? new FileInfo(localPath) : null;

            var status = DetermineStatus(remoteItem, localFile, forceRefresh);
            var reason = GetChangeReason(remoteItem, localFile, status, forceRefresh);

            items.Add(new DownloadItem
            {
                RemoteItem = remoteItem,
                LocalFile = localFile,
                Status = status,
                Reason = reason
            });
        }

        // Find orphaned files if requested
        var orphanedFiles = includeOrphans
            ? FindOrphanedFiles(destinationDirectory, remoteItems, config, includeAllOrphans)
            : [];

        // Calculate statistics
        var downloadSize = items
            .Where(i => i.Status is FileStatus.New or FileStatus.Modified)
            .Sum(i => i.RemoteItem.Size);

        var orphanedSize = orphanedFiles.Sum(f => f.Length);

        var stats = new DownloadStatistics
        {
            NewFiles = items.Count(i => i.Status == FileStatus.New),
            ModifiedFiles = items.Count(i => i.Status == FileStatus.Modified),
            UnchangedFiles = items.Count(i => i.Status == FileStatus.Unchanged),
            OrphanedFiles = orphanedFiles.Count,
            TotalDownloadSize = downloadSize,
            TotalOrphanedSize = orphanedSize
        };

        return new DownloadAnalysis
        {
            Items = items,
            OrphanedFiles = orphanedFiles,
            Statistics = stats
        };
    }

    /// <summary>
    /// Determines file status by comparing remote and local versions
    /// </summary>
    /// <param name="remote">Remote OneDrive item</param>
    /// <param name="local">Local file info (null if file doesn't exist)</param>
    /// <param name="forceRefresh">If true, treat all existing files as Modified</param>
    /// <remarks>
    /// Performance optimization: Check size FIRST (fast), only if size matches check LastModified.
    /// This avoids expensive date comparisons for files that obviously differ in size.
    /// </remarks>
    private static FileStatus DetermineStatus(OneDriveItem remote, FileInfo? local, bool forceRefresh)
    {
        if (local is null)
        {
            return FileStatus.New;
        }

        // Force refresh: treat all existing files as modified
        if (forceRefresh)
        {
            return FileStatus.Modified;
        }

        // 1. FIRST: Check size (fast, cheap operation)
        if (local.Length != remote.Size)
        {
            return FileStatus.Modified;
        }

        // 2. ONLY IF SIZE MATCHES: Check LastModified (more expensive)
        var timeDiff = Math.Abs((local.LastWriteTimeUtc - remote.LastModified).TotalSeconds);

        if (timeDiff > LastModifiedToleranceSeconds)
        {
            return FileStatus.Modified;
        }

        return FileStatus.Unchanged;
    }

    /// <summary>
    /// Gets human-readable reason for the file status
    /// </summary>
    private static string GetChangeReason(OneDriveItem remote, FileInfo? local, FileStatus status, bool forceRefresh)
    {
        return status switch
        {
            FileStatus.New => "New file",
            FileStatus.Unchanged => "Up to date",
            FileStatus.Modified when forceRefresh => "Forced refresh",
            FileStatus.Modified when local is not null && local.Length != remote.Size =>
                $"Size changed: {FileSizeFormatter.Format(local.Length)} → {FileSizeFormatter.Format(remote.Size)}",
            FileStatus.Modified when local is not null =>
                $"Modified: {local.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss} → {remote.LastModified:yyyy-MM-dd HH:mm:ss} UTC",
            FileStatus.Orphaned => "Orphaned",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Finds files that exist locally but not on OneDrive
    /// </summary>
    private static IReadOnlyList<FileInfo> FindOrphanedFiles(
        string destinationDirectory,
        IReadOnlyList<OneDriveItem> remoteItems,
        OneDriveConfig config,
        bool includeAll
    )
    {
        if (!Directory.Exists(destinationDirectory))
        {
            return [];
        }

        // Get all local files
        var allLocalFiles = Directory.EnumerateFiles(destinationDirectory, "*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .ToList();

        // Filter by patterns if not includeAll
        var localFiles = includeAll
            ? allLocalFiles
            : [.. allLocalFiles.Where(f => ShouldIncludeFile(f.Name, config.IncludePatterns, config.ExcludePatterns))];

        // Build set of remote file paths (relative)
        var remotePaths = new HashSet<string>(
            remoteItems
                .Where(i => !i.IsFolder)
                .Select(GetRelativePath),
            StringComparer.OrdinalIgnoreCase
        );

        // Find local files not in remote
        return [.. localFiles
            .Where(local =>
            {
                var relativePath = Path.GetRelativePath(destinationDirectory, local.FullName);
                return !remotePaths.Contains(relativePath);
            })];
    }

    /// <summary>
    /// Gets relative path for an OneDrive item
    /// </summary>
    private static string GetRelativePath(OneDriveItem item)
    {
        return string.IsNullOrEmpty(item.ParentPath)
            ? item.Name
            : Path.Combine(item.ParentPath, item.Name);
    }

    /// <summary>
    /// Checks if a file should be included based on patterns
    /// </summary>
    private static bool ShouldIncludeFile(
        string fileName,
        IReadOnlyList<string>? includePatterns,
        IReadOnlyList<string>? excludePatterns
    )
    {
        // Check exclude first
        if (excludePatterns is not null && excludePatterns.Any(p => MatchesWildcard(fileName, p)))
        {
            return false;
        }

        // Use defaults if no include patterns (images + markdown)
        if (includePatterns is null or [])
        {
            return DefaultExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        return includePatterns.Any(p => MatchesWildcard(fileName, p));
    }

    /// <summary>
    /// Simple wildcard matching (supports * and ?)
    /// </summary>
    private static bool MatchesWildcard(string text, string pattern)
    {
        var regex = RegexCache.GetOrAdd(pattern, p =>
        {
            var regexPattern = "^" +
                Regex.Escape(p)
                    .Replace("\\*", ".*", StringComparison.Ordinal)
                    .Replace("\\?", ".", StringComparison.Ordinal) +
                "$";
            return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        });

        return regex.IsMatch(text);
    }
}

