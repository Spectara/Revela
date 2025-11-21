using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
#pragma warning disable IDE0005 // Using directive is necessary for LoggerMessage attribute
using Microsoft.Extensions.Logging;
#pragma warning restore IDE0005
using Spectara.Revela.Plugin.Source.OneDrive.Models;

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
public sealed partial class SharedLinkProvider(
    HttpClient httpClient,
    ILogger<SharedLinkProvider> logger) : IOneDriveProvider
{
    private const string BadgerAppId = "5cbed6ac-a083-4e14-b191-b4ba07653de2";
    private const string BadgerTokenUrl = "https://api-badgerp.svc.ms/v1.0/token";
    private const string OneDriveApiBaseUrl = "https://api.onedrive.com/v1.0";

    private string? cachedToken;
    private DateTime tokenExpiry = DateTime.MinValue;

    /// <inheritdoc />
    public async Task<IReadOnlyList<OneDriveItem>> ListItemsAsync(
        OneDriveConfig config,
        CancellationToken cancellationToken = default
    )
    {
        LogListingItems(logger, config.ShareUrl);

        var token = await GetBadgerTokenAsync(cancellationToken);

        // Activate token and get share metadata (required for subfolder access)
        var metadata = await ActivateBadgerTokenAsync(config.ShareUrl, token, cancellationToken);

        var items = new List<OneDriveItem>();

        // List items recursively from root
        await ListItemsRecursiveAsync(config.ShareUrl, "", token, metadata, items, cancellationToken);

        LogItemsListed(logger, items.Count);
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

        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.Add("Authorization", $"Badger {token}");

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        if (jsonResponse is null)
        {
            return;
        }

        // Parse children
        if (jsonResponse.RootElement.TryGetProperty("value", out var valueArray))
        {
            foreach (var item in valueArray.EnumerateArray())
            {
                var oneDriveItem = ParseOneDriveItem(item, folderPath);
                if (oneDriveItem != null)
                {
                    items.Add(oneDriveItem);

                    // Recursively process subfolders
                    if (oneDriveItem.IsFolder)
                    {
                        var subPath = string.IsNullOrEmpty(folderPath)
                            ? oneDriveItem.Name
                            : $"{folderPath}/{oneDriveItem.Name}";

                        await ListItemsRecursiveAsync(shareUrl, subPath, token, metadata, items, cancellationToken);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
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

        LogDownloadingFile(logger, item.Name, destinationPath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Download file (OneDrive download URLs don't need Badger token)
        using var response = await httpClient.GetAsync(new Uri(item.DownloadUrl), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await stream.CopyToAsync(fileStream, cancellationToken);

        LogFileDownloaded(logger, item.Name, new FileInfo(destinationPath).Length);
        return destinationPath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> DownloadAllAsync(
        OneDriveConfig config,
        string destinationDirectory,
        bool forceRefresh = false,
        int concurrency = 6,
        IProgress<(int current, int total, string fileName)>? progress = null,
        IReadOnlyList<OneDriveItem>? preScannedItems = null,
        CancellationToken cancellationToken = default
    )
    {
        LogDownloadingAll(logger, config.ShareUrl);

        // Use pre-scanned items if available, otherwise scan now
        var allItems = preScannedItems ?? await ListItemsAsync(config, cancellationToken);

        // Debug: Log item types
        var folderCount = allItems.Count(i => i.IsFolder);
        var fileCount = allItems.Count(i => !i.IsFolder);
        LogItemTypes(logger, fileCount, folderCount);

        // Filter files using smart defaults (like original script)
        var filesOnly = allItems.Where(item => !item.IsFolder).ToList();

        // Use config patterns or defaults
        var useDefaults = config.IncludePatterns == null || config.IncludePatterns.Count == 0;

        if (useDefaults)
        {
            LogUsingDefaultFilters(logger);
        }

        var filteredItems = filesOnly
            .Where(item => ShouldIncludeFile(item, config.IncludePatterns, config.ExcludePatterns))
            .ToList();

        LogFilesFiltered(logger, filteredItems.Count, allItems.Count);

        var downloadedFiles = new List<string>();
        var current = 0;
        var total = filteredItems.Count;

        // Download files with parallelism (configurable concurrency)
        using var semaphore = new SemaphoreSlim(concurrency);
        var downloadTasks = filteredItems.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var currentIndex = Interlocked.Increment(ref current);

                // Build destination path with folder structure
                var relativePath = string.IsNullOrEmpty(item.ParentPath)
                    ? item.Name
                    : Path.Combine(item.ParentPath, item.Name);

                var destinationPath = Path.Combine(destinationDirectory, relativePath);

                // Report progress
                progress?.Report((currentIndex, total, relativePath));

                // Skip if file exists and not forcing refresh
                if (!forceRefresh && File.Exists(destinationPath))
                {
                    var fileInfo = new FileInfo(destinationPath);
                    if (fileInfo.Length == item.Size)
                    {
                        LogFileSkipped(logger, item.Name);
                        return destinationPath;
                    }
                }

                // Download file
                return await DownloadFileAsync(item, destinationPath, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(downloadTasks);
        downloadedFiles.AddRange(results);

        LogDownloadComplete(logger, downloadedFiles.Count);
        return downloadedFiles;
    }

    /// <summary>
    /// Gets a Badger authentication token (cached for 7 days)
    /// </summary>
    private async Task<string> GetBadgerTokenAsync(CancellationToken cancellationToken)
    {
        // Return cached token if still valid
        if (cachedToken != null && DateTime.UtcNow < tokenExpiry)
        {
            LogUsingCachedToken(logger);
            return cachedToken;
        }

        LogRequestingBadgerToken(logger);

        var requestBody = new { appId = BadgerAppId };
        var response = await httpClient.PostAsJsonAsync(BadgerTokenUrl, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<BadgerTokenResponse>(cancellationToken: cancellationToken);
        if (tokenResponse?.Token is null)
        {
            throw new InvalidOperationException("Failed to obtain Badger token");
        }

        cachedToken = tokenResponse.Token;
        tokenExpiry = DateTime.UtcNow.AddDays(6); // Cache for 6 days (token valid for 7)

        LogBadgerTokenReceived(logger);
        return cachedToken;
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

        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("Failed to get share metadata");

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

        LogBadgerTokenActivated(logger);
        LogShareMetadataReceived(logger, driveId, folderId);

        return new ShareMetadata { DriveId = driveId, FolderId = folderId };
    }

    /// <summary>
    /// Builds the OneDrive API URL for root folder
    /// </summary>
    private static string BuildRootApiUrl(string shareUrl)
    {
        // Encode share URL to base64url format
        var encodedUrl = EncodeShareUrl(shareUrl);

        // Select fields to retrieve (matches original script)
        const string selectFields = "name,description,@content.downloadUrl,file,folder,id";

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

        // Select fields to retrieve (matches original script)
        const string selectFields = "name,description,@content.downloadUrl,file,folder,id";

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

        var lastModified = element.TryGetProperty("lastModifiedDateTime", out var modifiedElement)
            ? DateTime.Parse(modifiedElement.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
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

    /// <summary>
    /// Determines if a file should be included based on patterns or smart defaults
    /// </summary>
    /// <remarks>
    /// Smart defaults (like original script):
    /// - All images (detected via MIME type: image/*)
    /// - All markdown files (*.md)
    /// </remarks>
    private static bool ShouldIncludeFile(OneDriveItem item, IReadOnlyList<string>? includePatterns, IReadOnlyList<string>? excludePatterns)
    {
        // Check exclude patterns first (if any)
        if (excludePatterns is not null && excludePatterns.Count > 0)
        {
            if (excludePatterns.Any(pattern => MatchesWildcard(item.Name, pattern)))
            {
                return false;
            }
        }

        // Use smart defaults if no include patterns specified
        if (includePatterns is null || includePatterns.Count == 0)
        {
            // Default 1: All images (via MIME type - like original script)
            if (!string.IsNullOrEmpty(item.MimeType) && item.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Default 2: All markdown files
            return item.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        }

        // Use custom patterns
        return includePatterns.Any(pattern => MatchesWildcard(item.Name, pattern));
    }

    /// <summary>
    /// Simple wildcard matching (supports * and ?)
    /// </summary>
    private static bool MatchesWildcard(string text, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Information, Message = "Listing items from OneDrive share: {shareUrl}")]
    private static partial void LogListingItems(ILogger logger, string shareUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listed {count} items from OneDrive")]
    private static partial void LogItemsListed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Requesting Badger authentication token")]
    private static partial void LogRequestingBadgerToken(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Badger token received successfully")]
    private static partial void LogBadgerTokenReceived(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Badger token activated successfully")]
    private static partial void LogBadgerTokenActivated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Share metadata received: DriveId={driveId}, FolderId={folderId}")]
    private static partial void LogShareMetadataReceived(ILogger logger, string driveId, string folderId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using cached Badger token")]
    private static partial void LogUsingCachedToken(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading file: {fileName} to {destination}")]
    private static partial void LogDownloadingFile(ILogger logger, string fileName, string destination);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloaded file: {fileName} ({size} bytes)")]
    private static partial void LogFileDownloaded(ILogger logger, string fileName, long size);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading all files from: {shareUrl}")]
    private static partial void LogDownloadingAll(ILogger logger, string shareUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {fileCount} files and {folderCount} folders")]
    private static partial void LogItemTypes(ILogger logger, int fileCount, int folderCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using smart defaults: all images (via MIME type) and markdown files")]
    private static partial void LogUsingDefaultFilters(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Filtered {filteredCount} files out of {totalCount} items")]
    private static partial void LogFilesFiltered(ILogger logger, int filteredCount, int totalCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping existing file: {fileName}")]
    private static partial void LogFileSkipped(ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Download complete: {count} files")]
    private static partial void LogDownloadComplete(ILogger logger, int count);
}
