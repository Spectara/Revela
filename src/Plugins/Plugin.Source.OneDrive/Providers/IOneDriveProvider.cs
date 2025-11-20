using Spectara.Revela.Plugin.Source.OneDrive.Models;

namespace Spectara.Revela.Plugin.Source.OneDrive.Providers;

/// <summary>
/// Interface for OneDrive data providers
/// </summary>
public interface IOneDriveProvider
{
    /// <summary>
    /// Lists all items in the shared folder
    /// </summary>
    /// <param name="config">OneDrive configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of OneDrive items</returns>
    Task<IReadOnlyList<OneDriveItem>> ListItemsAsync(
        OneDriveConfig config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Downloads a file from OneDrive to local cache
    /// </summary>
    /// <param name="item">The item to download</param>
    /// <param name="destinationPath">Local destination path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Local file path</returns>
    Task<string> DownloadFileAsync(
        OneDriveItem item,
        string destinationPath,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Downloads all matching files from the shared folder
    /// </summary>
    /// <param name="config">OneDrive configuration</param>
    /// <param name="destinationDirectory">Local destination directory</param>
    /// <param name="forceRefresh">Force re-download even if files exist</param>
    /// <param name="concurrency">Number of parallel downloads</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="preScannedItems">Optional pre-scanned items to avoid re-scanning</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of downloaded file paths</returns>
    Task<IReadOnlyList<string>> DownloadAllAsync(
        OneDriveConfig config,
        string destinationDirectory,
        bool forceRefresh = false,
        int concurrency = 6,
        IProgress<(int current, int total, string fileName)>? progress = null,
        IReadOnlyList<OneDriveItem>? preScannedItems = null,
        CancellationToken cancellationToken = default
    );
}
