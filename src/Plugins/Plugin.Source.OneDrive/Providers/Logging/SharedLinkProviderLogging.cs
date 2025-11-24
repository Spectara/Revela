namespace Spectara.Revela.Plugin.Source.OneDrive.Providers.Logging;

/// <summary>
/// High-performance logging for SharedLinkProvider using source-generated extension methods
/// </summary>
internal static partial class SharedLinkProviderLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Listing items from OneDrive share: {shareUrl}")]
    public static partial void ListingItems(this ILogger logger, string shareUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listed {count} items from OneDrive")]
    public static partial void ItemsListed(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Requesting Badger authentication token")]
    public static partial void RequestingBadgerToken(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Badger token received successfully")]
    public static partial void BadgerTokenReceived(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Badger token activated successfully")]
    public static partial void BadgerTokenActivated(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Share metadata received: DriveId={driveId}, FolderId={folderId}")]
    public static partial void ShareMetadataReceived(this ILogger logger, string driveId, string folderId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading file: {fileName} to {destination}")]
    public static partial void DownloadingFile(this ILogger logger, string fileName, string destination);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloaded file: {fileName} ({size} bytes)")]
    public static partial void FileDownloaded(this ILogger logger, string fileName, long size);
}
