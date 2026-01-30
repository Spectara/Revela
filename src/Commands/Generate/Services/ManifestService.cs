using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;

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
/// Cache directory is determined from project environment.
/// </para>
/// </remarks>
public sealed partial class ManifestService(
    ILogger<ManifestService> logger,
    IOptions<ProjectEnvironment> projectEnvironment) : IManifestRepository
{
    private const string ManifestFileName = "manifest.json";
    private const string CacheDirectoryName = ".cache";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never  // Always serialize all properties
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
    public string ScanConfigHash
    {
        get => manifest.Meta.ScanConfigHash;
        set => manifest.Meta.ScanConfigHash = value;
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

    /// <inheritdoc />
    public IReadOnlyDictionary<string, int> FormatQualities => manifest.Meta.FormatQualities;

    /// <inheritdoc />
    public void SetFormatQualities(IReadOnlyDictionary<string, int> qualities) => manifest.Meta.FormatQualities = new Dictionary<string, int>(qualities);

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

    private string GetCacheDirectory() => Path.Combine(projectEnvironment.Value.Path, CacheDirectoryName);

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

        // Normalize to forward slashes for cross-platform comparison
        directoryPath = NormalizePath(directoryPath);

        return FindNodeByPath(manifest.Root, directoryPath);
    }

    /// <summary>
    /// Find a node by its filesystem path.
    /// </summary>
    private static ManifestEntry? FindNodeByPath(ManifestEntry node, string path)
    {
        // Normalize both paths for cross-platform comparison
        var normalizedNodePath = NormalizePath(node.Path);
        var normalizedSearchPath = NormalizePath(path);

        if (string.Equals(normalizedNodePath, normalizedSearchPath, StringComparison.OrdinalIgnoreCase))
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
    /// Normalize path separators to forward slashes for cross-platform comparison.
    /// </summary>
    private static string NormalizePath(string path) => path.Replace('\\', '/');

    /// <summary>
    /// Get the full source path for an image in a node.
    /// </summary>
    /// <remarks>
    /// Uses forward slashes for consistency with manifest key format.
    /// For filtered images (e.g., homepage showing images from other galleries),
    /// SourcePath contains the original location. For regular images,
    /// SourcePath equals nodePath + Filename.
    /// </remarks>
    private static string GetImageSourcePath(string nodePath, GalleryContent content)
    {
        // If content has SourcePath set, use it directly (handles filtered images)
        if (content is ImageContent image && !string.IsNullOrEmpty(image.SourcePath))
        {
            return image.SourcePath.Replace('\\', '/');
        }

        // Fall back to nodePath + Filename for backward compatibility
        var path = string.IsNullOrEmpty(nodePath)
            ? content.Filename
            : $"{nodePath}/{content.Filename}";
        return path.Replace('\\', '/');
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Compute hash for image processing configuration.
    /// </summary>
    /// <remarks>
    /// When this hash changes, all images need to be regenerated.
    /// </remarks>
    public static string ComputeConfigHash(
        IReadOnlyList<int> sizes,
        IReadOnlyDictionary<string, int> formats)
    {
        var sizesStr = string.Join(",", sizes.OrderBy(s => s));
        var formatsStr = string.Join(",", formats.OrderBy(f => f.Key).Select(f => $"{f.Key}:{f.Value}"));
        var input = $"sizes:{sizesStr}|formats:{formatsStr}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..12];
    }

    /// <summary>
    /// Compute hash for scan configuration.
    /// </summary>
    /// <remarks>
    /// When this hash changes, all metadata needs to be re-read from source files.
    /// Includes: placeholder strategy, min dimensions.
    /// </remarks>
    public static string ComputeScanConfigHash(
        PlaceholderStrategy placeholderStrategy,
        int minWidth,
        int minHeight)
    {
        var input = $"placeholder:{placeholderStrategy}|minWidth:{minWidth}|minHeight:{minHeight}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..12];
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
