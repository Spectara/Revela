namespace Spectara.Revela.Plugin.Source.OneDrive.Providers.Logging;

/// <summary>
/// High-performance logging for SharedLinkProvider using source-generated extension methods
/// </summary>
internal static partial class SharedLinkProviderLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Listing items from OneDrive share: {shareUrl}")]
    public static partial void ListingItems(this ILogger<SharedLinkProvider> logger, string shareUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listed {count} items from OneDrive")]
    public static partial void ItemsListed(this ILogger<SharedLinkProvider> logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Requesting Badger authentication token")]
    public static partial void RequestingBadgerToken(this ILogger<SharedLinkProvider> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Badger token received successfully")]
    public static partial void BadgerTokenReceived(this ILogger<SharedLinkProvider> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Badger token activated successfully")]
    public static partial void BadgerTokenActivated(this ILogger<SharedLinkProvider> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Share metadata received: DriveId={driveId}, FolderId={folderId}")]
    public static partial void ShareMetadataReceived(this ILogger<SharedLinkProvider> logger, string driveId, string folderId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading file: {fileName} to {destination}")]
    public static partial void DownloadingFile(this ILogger<SharedLinkProvider> logger, string fileName, string destination);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloaded file: {fileName} ({size} bytes)")]
    public static partial void FileDownloaded(this ILogger<SharedLinkProvider> logger, string fileName, long size);
}
