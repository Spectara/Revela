namespace Spectara.Revela.Plugin.Source.OneDrive.Models;

/// <summary>
/// Analysis result of comparing local files with OneDrive
/// </summary>
internal sealed class DownloadAnalysis
{
    /// <summary>
    /// All items analyzed (New, Modified, Unchanged)
    /// </summary>
    public required IReadOnlyList<DownloadItem> Items { get; init; }

    /// <summary>
    /// Files that exist locally but not on OneDrive
    /// </summary>
    public required IReadOnlyList<FileInfo> OrphanedFiles { get; init; }

    /// <summary>
    /// Statistics about the analysis
    /// </summary>
    public required DownloadStatistics Statistics { get; init; }

    /// <summary>
    /// Items that need to be downloaded (New or Modified)
    /// </summary>
    public IEnumerable<DownloadItem> ItemsToDownload =>
        Items.Where(i => i.Status is FileStatus.New or FileStatus.Modified);

    /// <summary>
    /// Items that are unchanged (will be skipped)
    /// </summary>
    public IEnumerable<DownloadItem> UnchangedItems =>
        Items.Where(i => i.Status == FileStatus.Unchanged);
}
