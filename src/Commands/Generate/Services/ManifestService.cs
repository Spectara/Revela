using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Manifest;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Service for managing the site manifest file.
/// </summary>
/// <remarks>
/// <para>
/// The manifest enables incremental builds by:
/// - Tracking source image hashes for change detection
/// - Storing generated sizes/formats for templates
/// - Caching EXIF data (eliminates need for separate ExifCache)
/// - Tracking config changes (forces full rebuild when sizes change)
/// </para>
/// <para>
/// Uses a unified tree structure with Root node containing the entire site.
/// Image lookups are cached internally for O(1) access by source path.
/// </para>
/// <para>
/// Holds manifest state in memory, persists on SaveAsync.
/// Cache directory is determined from current working directory.
/// </para>
/// </remarks>
public sealed partial class ManifestService(ILogger<ManifestService> logger) : IManifestRepository
{
    private const string ManifestFileName = "manifest.json";
    private const string CacheDirectoryName = ".cache";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private ImageManifest manifest = new();

    /// <summary>
    /// Internal cache for O(1) image lookups by source path.
    /// </summary>
    /// <remarks>
    /// Built from traversing the tree on load/setRoot.
    /// Keys are normalized source paths (forward slashes).
    /// Values are tuples of (ImageContent, containing ManifestEntry node).
    /// </remarks>
    private Dictionary<string, (ImageContent Entry, ManifestEntry Node)> imageCache = [];

    #region Root Node

    /// <inheritdoc />
    public ManifestEntry? Root => manifest.Root;

    /// <inheritdoc />
    public void SetRoot(ManifestEntry root)
    {
        manifest.Root = root;
        RebuildImageCache();
    }

    #endregion

    #region Image Entries

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ImageContent> Images =>
        imageCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);

    /// <inheritdoc />
    public ImageContent? GetImage(string sourcePath) => imageCache.TryGetValue(sourcePath, out var cached) ? cached.Entry : null;

    /// <inheritdoc />
    public void SetImage(string sourcePath, ImageContent entry)
    {
        // Find the node that should contain this image (based on path prefix)
        var node = FindNodeForImage(sourcePath);
        if (node is null)
        {
            LogImageNodeNotFound(logger, sourcePath);
            return;
        }

        // Check if image already exists in node
        var existingIndex = node.Content
            .Select((c, i) => (Content: c, Index: i))
            .Where(x => x.Content is ImageContent)
            .FirstOrDefault(x => GetImageSourcePath(node.Path, x.Content) == sourcePath)
            .Index;

        // FirstOrDefault returns 0 for Index if not found, so we need to check separately
        var existingImage = node.Content.OfType<ImageContent>()
            .FirstOrDefault(img => GetImageSourcePath(node.Path, img) == sourcePath);

        if (existingImage is not null)
        {
            var actualIndex = node.Content.IndexOf(existingImage);
            node.Content[actualIndex] = entry;
        }
        else
        {
            node.Content.Add(entry);
        }

        // Update cache
        imageCache[sourcePath] = (entry, node);
    }

    /// <inheritdoc />
    public bool RemoveImage(string sourcePath)
    {
        if (!imageCache.TryGetValue(sourcePath, out var cached))
        {
            return false;
        }

        // Remove from node
        var removed = cached.Node.Content.Remove(cached.Entry);

        // Remove from cache
        imageCache.Remove(sourcePath);
        return removed;
    }

    #endregion

    #region Metadata

    /// <inheritdoc />
    public string ConfigHash
    {
        get => manifest.Meta.ConfigHash;
        set => manifest.Meta.ConfigHash = value;
    }

    /// <inheritdoc />
    public DateTime? LastScanned
    {
        get => manifest.Meta.LastScanned;
        set => manifest.Meta.LastScanned = value;
    }

    /// <inheritdoc />
    public DateTime? LastImagesProcessed
    {
        get => manifest.Meta.LastImagesProcessed;
        set => manifest.Meta.LastImagesProcessed = value;
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var cacheDirectory = GetCacheDirectory();
        var manifestPath = Path.Combine(cacheDirectory, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            LogManifestNotFound(logger, manifestPath);
            manifest = new ImageManifest();
            imageCache.Clear();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var loaded = JsonSerializer.Deserialize<ImageManifest>(json, JsonOptions);

            if (loaded is null)
            {
                LogManifestInvalid(logger, manifestPath);
                manifest = new ImageManifest();
                imageCache.Clear();
            }
            else
            {
                manifest = loaded;
                RebuildImageCache();
                LogManifestLoaded(logger, imageCache.Count);
            }
        }
        catch (JsonException ex)
        {
            LogManifestParseError(logger, manifestPath, ex);
            manifest = new ImageManifest();
            imageCache.Clear();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var cacheDirectory = GetCacheDirectory();
        Directory.CreateDirectory(cacheDirectory);

        var manifestPath = Path.Combine(cacheDirectory, ManifestFileName);
        var tempPath = manifestPath + ".tmp";

        // Update timestamp
        manifest.Meta.LastUpdated = DateTime.UtcNow;

        try
        {
            // Write to temp file first
            var json = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            // Atomic rename
            File.Move(tempPath, manifestPath, overwrite: true);

            LogManifestSaved(logger, imageCache.Count, manifestPath);
        }
        catch (Exception ex)
        {
            LogManifestSaveError(logger, manifestPath, ex);

            // Cleanup temp file
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            throw;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        manifest = new ImageManifest();
        imageCache.Clear();
    }

    #endregion

    #region Utilities

    /// <inheritdoc />
    public IReadOnlyList<string> RemoveOrphans(IReadOnlySet<string> existingSourcePaths)
    {
        var orphans = imageCache.Keys
            .Where(key => !existingSourcePaths.Contains(key))
            .ToList();

        foreach (var orphan in orphans)
        {
            RemoveImage(orphan);
            LogOrphanRemoved(logger, orphan);
        }

        if (orphans.Count > 0)
        {
            LogOrphansRemoved(logger, orphans.Count);
        }

        return orphans;
    }

    private static string GetCacheDirectory() => Path.Combine(Environment.CurrentDirectory, CacheDirectoryName);

    /// <summary>
    /// Rebuild the internal image cache by traversing the tree.
    /// </summary>
    private void RebuildImageCache()
    {
        imageCache = [];

        if (manifest.Root is null)
        {
            return;
        }

        TraverseForImages(manifest.Root);
    }

    /// <summary>
    /// Recursively traverse the tree to populate image cache.
    /// </summary>
    private void TraverseForImages(ManifestEntry node)
    {
        foreach (var image in node.Content.OfType<ImageContent>())
        {
            var sourcePath = GetImageSourcePath(node.Path, image);
            imageCache[sourcePath] = (image, node);
        }

        foreach (var child in node.Children)
        {
            TraverseForImages(child);
        }
    }

    /// <summary>
    /// Find the node that should contain an image based on its source path.
    /// </summary>
    private ManifestEntry? FindNodeForImage(string sourcePath)
    {
        if (manifest.Root is null)
        {
            return null;
        }

        // Extract directory path from source path
        var lastSeparator = sourcePath.LastIndexOfAny(['/', '\\']);
        var directoryPath = lastSeparator > 0 ? sourcePath[..lastSeparator] : string.Empty;

        // Normalize to backslashes for comparison with node paths
        directoryPath = directoryPath.Replace('/', '\\');

        return FindNodeByPath(manifest.Root, directoryPath);
    }

    /// <summary>
    /// Find a node by its filesystem path.
    /// </summary>
    private static ManifestEntry? FindNodeByPath(ManifestEntry node, string path)
    {
        if (string.Equals(node.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindNodeByPath(child, path);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the full source path for an image in a node.
    /// </summary>
    private static string GetImageSourcePath(string nodePath, GalleryContent content)
    {
        return string.IsNullOrEmpty(nodePath)
            ? content.Filename
            : $"{nodePath}\\{content.Filename}";
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Compute hash for a source image file.
    /// </summary>
    /// <remarks>
    /// Uses filename + lastWriteTime + fileSize for fast change detection.
    /// Same approach as expose.sh (lines 409-472).
    /// </remarks>
#pragma warning disable CA5351 // MD5 is used for caching, not security
    public static string ComputeSourceHash(string imagePath)
    {
        var fileInfo = new FileInfo(imagePath);
        var input = $"{fileInfo.Name}_{fileInfo.LastWriteTimeUtc.Ticks}_{fileInfo.Length}";
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..12];
    }
#pragma warning restore CA5351

    /// <summary>
    /// Compute hash for image processing configuration.
    /// </summary>
    /// <remarks>
    /// When this hash changes, all images need to be regenerated.
    /// </remarks>
#pragma warning disable CA5351 // MD5 is used for caching, not security
    public static string ComputeConfigHash(
        IReadOnlyList<int> sizes,
        IReadOnlyDictionary<string, int> formats)
    {
        var sizesStr = string.Join(",", sizes.OrderBy(s => s));
        var formatsStr = string.Join(",", formats.OrderBy(f => f.Key).Select(f => $"{f.Key}:{f.Value}"));
        var input = $"sizes:{sizesStr}|formats:{formatsStr}";
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..12];
    }
#pragma warning restore CA5351

    /// <summary>
    /// Check if an image needs to be processed based on hash comparison.
    /// </summary>
    /// <param name="existingEntry">Existing manifest entry (null if new image)</param>
    /// <param name="sourceHash">Hash of source image</param>
    /// <returns>True if image needs processing, false if unchanged</returns>
    public static bool NeedsProcessing(ImageContent? existingEntry, string sourceHash)
    {
        if (existingEntry is null)
        {
            return true; // New image
        }

        return existingEntry.Hash != sourceHash; // Changed if hash differs
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Manifest not found at {Path}, starting fresh")]
    private static partial void LogManifestNotFound(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Manifest at {Path} is invalid, starting fresh")]
    private static partial void LogManifestInvalid(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded manifest with {Count} images")]
    private static partial void LogManifestLoaded(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse manifest at {Path}")]
    private static partial void LogManifestParseError(ILogger logger, string path, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved manifest with {Count} images to {Path}")]
    private static partial void LogManifestSaved(ILogger logger, int count, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save manifest to {Path}")]
    private static partial void LogManifestSaveError(ILogger logger, string path, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed orphan manifest entry: {SourcePath}")]
    private static partial void LogOrphanRemoved(ILogger logger, string sourcePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {Count} orphaned manifest entries")]
    private static partial void LogOrphansRemoved(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not find node for image: {SourcePath}")]
    private static partial void LogImageNodeNotFound(ILogger logger, string sourcePath);

    #endregion
}
