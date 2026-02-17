using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Plugin.Source.OneDrive.Providers.Logging;

namespace Spectara.Revela.Plugin.Source.OneDrive.Providers;

/// <summary>
/// OneDrive provider for public shared links using Microsoft Badger API (no OAuth required)
/// </summary>
/// <remarks>
/// Uses Microsoft's internal Badger authentication service to access shared OneDrive folders.
/// App ID: 5cbed6ac-a083-4e14-b191-b4ba07653de2 (Microsoft's OneDrive web interface)
/// Based on: https://github.com/eugenenuke/onedrive-downloader
///
/// Uses C# 12 Primary Constructor - parameters are captured automatically.
/// HttpClient is injected as a Typed Client (configured in OneDrivePlugin.ConfigureServices).
/// </remarks>
public sealed class SharedLinkProvider(
    HttpClient httpClient,
    ILogger<SharedLinkProvider> logger)
{
    private const string BadgerAppId = "5cbed6ac-a083-4e14-b191-b4ba07653de2";
    private const string BadgerTokenUrl = "https://api-badgerp.svc.ms/v1.0/token";
    private const string OneDriveApiBaseUrl = "https://api.onedrive.com/v1.0";

    /// <summary>
    /// Lists all items in the shared folder recursively
    /// </summary>
    /// <param name="shareUrl">OneDrive share URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Share URLs with tokens don't always parse as valid System.Uri")]
    public async Task<IReadOnlyList<OneDriveItem>> ListItemsAsync(
        string shareUrl,
        CancellationToken cancellationToken = default
    )
    {
        logger.ListingItems(shareUrl);

        var token = await GetBadgerTokenAsync(cancellationToken);

        // Activate token and get share metadata (required for subfolder access)
        var metadata = await ActivateBadgerTokenAsync(shareUrl, token, cancellationToken);

        List<OneDriveItem> items = [];

        // List items recursively from root
        await ListItemsRecursiveAsync(shareUrl, "", token, metadata, items, cancellationToken);

        logger.ItemsListed(items.Count);
        return items;
    }

    /// <summary>
    /// Recursively lists items from a folder
    /// </summary>
    private async Task ListItemsRecursiveAsync(
        string shareUrl,
        string folderPath,
        string token,
        ShareMetadata metadata,
        List<OneDriveItem> items,
        CancellationToken cancellationToken
    )
    {
        // Build API URL: Root uses share-based URL, subfolders use drive-based URL
        string apiUrl;
        if (string.IsNullOrEmpty(folderPath))
        {
            // Root folder: /shares/{shareId}/root/children
            apiUrl = BuildRootApiUrl(shareUrl);
        }
        else
        {
            // Subfolder: /drives/{driveId}/items/{folderId}:/{path}:/children
            apiUrl = BuildSubfolderApiUrl(metadata.DriveId, metadata.FolderId, folderPath);
        }

        // Collect folders to process recursively (after all pages)
        var foldersToProcess = new List<(string Name, string SubPath)>();

        // Handle pagination - OneDrive API returns max ~200 items per page
        var nextLink = apiUrl;
        while (nextLink is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextLink);
            request.Headers.Add("Authorization", $"Badger {token}");

            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var jsonResponse = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            if (jsonResponse is null)
            {
                break;
            }

            // Parse children
            if (jsonResponse.RootElement.TryGetProperty("value", out var valueArray))
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    var oneDriveItem = ParseOneDriveItem(item, folderPath);
                    if (oneDriveItem is not null)
                    {
                        items.Add(oneDriveItem);

                        // Collect folders for later recursive processing
                        if (oneDriveItem.IsFolder)
                        {
                            var subPath = string.IsNullOrEmpty(folderPath)
                                ? oneDriveItem.Name
                                : $"{folderPath}/{oneDriveItem.Name}";

                            foldersToProcess.Add((oneDriveItem.Name, subPath));
                        }
                    }
                }
            }

            // Check for next page (@odata.nextLink)
            nextLink = jsonResponse.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkElement)
                ? nextLinkElement.GetString()
                : null;

            if (nextLink is not null)
            {
                logger.FetchingNextPage(items.Count);
            }
        }

        // Process all subfolders after pagination is complete
        foreach (var (_, subPath) in foldersToProcess)
        {
            await ListItemsRecursiveAsync(shareUrl, subPath, token, metadata, items, cancellationToken);
        }
    }

    /// <summary>
    /// Downloads a file from OneDrive to local path
    /// </summary>
    /// <remarks>
    /// Downloads use pre-signed CDN URLs (item.DownloadUrl) which are direct links to OneDrive's CDN.
    /// These URLs do NOT require the Badger token and do NOT count against OneDrive API rate limits.
    /// The URLs look like: https://public.am.files.1drv.com/y4m...?download&amp;...
    /// </remarks>
    public async Task<string> DownloadFileAsync(
        OneDriveItem item,
        string destinationPath,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(item.DownloadUrl))
        {
            throw new ArgumentException("Item does not have a download URL", nameof(item));
        }

        logger.DownloadingFile(item.Name, destinationPath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Download file from CDN (pre-signed URL, no Badger token needed, no API rate limit)
        using var response = await httpClient.GetAsync(new Uri(item.DownloadUrl), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        // Preserve OneDrive's LastModified timestamp (must be done after file is closed)
        File.SetLastWriteTimeUtc(destinationPath, item.LastModified);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.FileDownloaded(item.Name, new FileInfo(destinationPath).Length);
        }

        return destinationPath;
    }

    /// <summary>
    /// Gets a Badger authentication token from Microsoft API
    /// </summary>
    /// <remarks>
    /// Token is fetched fresh for each command execution (no caching).
    /// This matches the behavior of the original Bash script and keeps the implementation simple.
    /// The token request adds ~200ms overhead, which is negligible compared to the scan/download time.
    /// </remarks>
    private async Task<string> GetBadgerTokenAsync(CancellationToken cancellationToken)
    {
        logger.RequestingBadgerToken();

        var requestBody = new { appId = BadgerAppId };
        var response = await httpClient.PostAsJsonAsync(BadgerTokenUrl, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<BadgerTokenResponse>(cancellationToken: cancellationToken);
        if (tokenResponse?.Token is null)
        {
            throw new InvalidOperationException("Failed to obtain Badger token");
        }

        logger.BadgerTokenReceived();
        return tokenResponse.Token;
    }

    /// <summary>
    /// Activates the Badger token and retrieves share metadata
    /// Required before making API calls to shared folders
    /// </summary>
    /// <returns>Share metadata containing driveId and folderId for subfolder access</returns>
    private async Task<ShareMetadata> ActivateBadgerTokenAsync(string shareUrl, string token, CancellationToken cancellationToken)
    {
        var encodedUrl = EncodeShareUrl(shareUrl);
        var activationUrl = $"{OneDriveApiBaseUrl}/shares/u!{encodedUrl}/driveItem";

        using var request = new HttpRequestMessage(HttpMethod.Get, activationUrl);
        request.Headers.Add("Authorization", $"Badger {token}");
        request.Headers.Add("Prefer", "autoredeem");

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var jsonResponse = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("Failed to get share metadata");

        // Extract driveId and folderId from metadata
        var driveId = jsonResponse.RootElement.TryGetProperty("parentReference", out var parentRef) &&
                      parentRef.TryGetProperty("driveId", out var driveIdElement)
            ? driveIdElement.GetString() ?? string.Empty
            : string.Empty;

        var folderId = jsonResponse.RootElement.TryGetProperty("id", out var idElement)
            ? idElement.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrEmpty(driveId) || string.IsNullOrEmpty(folderId))
        {
            throw new InvalidOperationException("Could not extract drive/folder IDs from share metadata");
        }

        logger.BadgerTokenActivated();
        logger.ShareMetadataReceived(driveId, folderId);

        return new ShareMetadata { DriveId = driveId, FolderId = folderId };
    }

    /// <summary>
    /// Builds the OneDrive API URL for root folder
    /// </summary>
    private static string BuildRootApiUrl(string shareUrl)
    {
        // Encode share URL to base64url format
        var encodedUrl = EncodeShareUrl(shareUrl);

        // Select fields to retrieve (includes fileSystemInfo for accurate LastModified timestamp)
        const string selectFields = "name,description,@content.downloadUrl,file,folder,id,size,fileSystemInfo";

        return $"{OneDriveApiBaseUrl}/shares/u!{encodedUrl}/root/children?$select={selectFields}";
    }

    /// <summary>
    /// Builds the OneDrive API URL for subfolder using drive-based addressing
    /// </summary>
    /// <remarks>
    /// Uses /drives/{driveId}/items/{folderId}:/{path}:/children format
    /// Required for subfolder access after obtaining share metadata
    /// </remarks>
    private static string BuildSubfolderApiUrl(string driveId, string folderId, string folderPath)
    {
        // URL encode the path but preserve forward slashes
        var encodedPath = UrlEncodePath(folderPath);

        // Select fields to retrieve (includes fileSystemInfo for accurate LastModified timestamp)
        const string selectFields = "name,description,@content.downloadUrl,file,folder,id,size,fileSystemInfo";

        return $"{OneDriveApiBaseUrl}/drives/{driveId}/items/{folderId}:/{encodedPath}:/children?$select={selectFields}";
    }

    /// <summary>
    /// URL encodes a path while preserving forward slashes
    /// </summary>
    private static string UrlEncodePath(string path)
    {
        // Split by / to preserve folder structure
        var segments = path.Split('/');
        var encodedSegments = segments.Select(Uri.EscapeDataString);
        return string.Join("/", encodedSegments);
    }

    /// <summary>
    /// Encodes a share URL to base64url format (Microsoft's format)
    /// </summary>
    private static string EncodeShareUrl(string shareUrl)
    {
        var bytes = Encoding.UTF8.GetBytes(shareUrl);
        var base64 = Convert.ToBase64String(bytes);

        // Convert to base64url: remove padding, replace / with _, + with -
        return base64.TrimEnd('=').Replace('/', '_').Replace('+', '-');
    }

    /// <summary>
    /// Parses a JSON element into an OneDriveItem
    /// </summary>
    private static OneDriveItem? ParseOneDriveItem(JsonElement element, string parentPath)
    {
        if (!element.TryGetProperty("id", out var idElement) ||
            !element.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        var isFolder = element.TryGetProperty("folder", out _);
        var size = element.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0;

        // Get download URL from @content.downloadUrl or @microsoft.graph.downloadUrl
        var downloadUrl = element.TryGetProperty("@content.downloadUrl", out var contentDownloadElement)
            ? contentDownloadElement.GetString()
            : element.TryGetProperty("@microsoft.graph.downloadUrl", out var graphDownloadElement)
                ? graphDownloadElement.GetString()
                : null;

        var lastModified = element.TryGetProperty("fileSystemInfo", out var fileSystemInfoElement) &&
                           fileSystemInfoElement.TryGetProperty("lastModifiedDateTime", out var modifiedElement)
            ? DateTime.Parse(modifiedElement.GetString()!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal)
            : DateTime.UtcNow;

        var mimeType = element.TryGetProperty("file", out var fileElement) &&
                       fileElement.TryGetProperty("mimeType", out var mimeElement)
            ? mimeElement.GetString()
            : null;

        return new OneDriveItem
        {
            Id = idElement.GetString()!,
            Name = nameElement.GetString()!,
            IsFolder = isFolder,
            Size = size,
            DownloadUrl = downloadUrl,
            LastModified = lastModified,
            MimeType = mimeType,
            ParentPath = parentPath
        };
    }

    /// <summary>
    /// Badger token response model
    /// </summary>
    private sealed class BadgerTokenResponse
    {
        public string? Token { get; set; }
    }

    /// <summary>
    /// Share metadata model containing drive and folder IDs
    /// </summary>
    private sealed class ShareMetadata
    {
        public string DriveId { get; init; } = string.Empty;
        public string FolderId { get; init; } = string.Empty;
    }
}

