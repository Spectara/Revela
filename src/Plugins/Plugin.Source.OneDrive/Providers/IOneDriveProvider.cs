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
}
