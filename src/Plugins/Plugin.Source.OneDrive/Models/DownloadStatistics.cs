namespace Spectara.Revela.Plugin.Source.OneDrive.Models;

/// <summary>
/// Statistics about the download analysis
/// </summary>
public sealed class DownloadStatistics
{
    /// <summary>
    /// Number of files that exist only on OneDrive (will be downloaded)
    /// </summary>
    public int NewFiles { get; init; }

    /// <summary>
    /// Number of files that differ from OneDrive version (will be updated)
    /// </summary>
    public int ModifiedFiles { get; init; }

    /// <summary>
    /// Number of files that are up-to-date (will be skipped)
    /// </summary>
    public int UnchangedFiles { get; init; }

    /// <summary>
    /// Number of files that exist locally but not on OneDrive
    /// </summary>
    public int OrphanedFiles { get; init; }

    /// <summary>
    /// Total files that need to be downloaded (New + Modified)
    /// </summary>
    public int TotalFilesToDownload => NewFiles + ModifiedFiles;

    /// <summary>
    /// Total files analyzed
    /// </summary>
    public int TotalFiles => NewFiles + ModifiedFiles + UnchangedFiles;

    /// <summary>
    /// Total size of files to download (in bytes)
    /// </summary>
    public long TotalDownloadSize { get; init; }

    /// <summary>
    /// Total size of orphaned files (in bytes)
    /// </summary>
    public long TotalOrphanedSize { get; init; }
}
