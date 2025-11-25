namespace Spectara.Revela.Plugin.Source.OneDrive.Models;

/// <summary>
/// Represents a file to download with its sync status
/// </summary>
public sealed class DownloadItem
{
    /// <summary>
    /// Remote OneDrive item
    /// </summary>
    public required OneDriveItem RemoteItem { get; init; }

    /// <summary>
    /// Local file info (null if file doesn't exist locally)
    /// </summary>
    public FileInfo? LocalFile { get; init; }

    /// <summary>
    /// Status of this file (New, Modified, Unchanged)
    /// </summary>
    public required FileStatus Status { get; init; }

    /// <summary>
    /// Human-readable reason for the status
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Relative path for display (normalized to forward slashes)
    /// </summary>
    public string RelativePath => string.IsNullOrEmpty(RemoteItem.ParentPath)
        ? RemoteItem.Name
        : Path.Combine(RemoteItem.ParentPath, RemoteItem.Name).Replace('\\', '/');
}
