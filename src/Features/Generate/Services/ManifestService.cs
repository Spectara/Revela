using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Features.Generate.Services;

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
internal sealed partial class ManifestService(
    ILogger<ManifestService> logger,
    IOptions<ProjectEnvironment> projectEnvironment,
    TimeProvider timeProvider) : IManifestRepository
{
    private const string ManifestFileName = "manifest.json";

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

    /// <summary>
    /// Mutable working copy of <see cref="ManifestMeta.ProcessedImages"/>, written back on save.
    /// </summary>
    private Dictionary<string, string> processedImages = new(StringComparer.Ordinal);

    #region Root Node

    /// <inheritdoc />
    public ManifestEntry? Root => manifest.Root;

    /// <inheritdoc />
    public void SetRoot(ManifestEntry root)
    {
        manifest = manifest with { Root = root };
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
        if (manifest.Root is null)
        {
            LogImageNodeNotFound(logger, sourcePath);
            return;
        }

        // Find the node that should contain this image (based on path prefix)
        var node = FindNodeForImage(sourcePath);
        if (node is null)
        {
            LogImageNodeNotFound(logger, sourcePath);
            return;
        }

        // Build new content list (replace existing image or append new)
        var existingImage = node.Content
            .FirstOrDefault(img => GetImageSourcePath(node.Path, img) == sourcePath);

        var newContent = existingImage is not null
            ? node.Content.Select(c => ReferenceEquals(c, existingImage) ? entry : c).ToList()
            : [.. node.Content, entry];

        // Build new immutable node and update tree
        var newNode = node with { Content = newContent };
        var newRoot = ReplaceNodeInTree(manifest.Root, node, newNode);
        manifest = manifest with { Root = newRoot };

        // Rebuild cache to reflect new node references
        RebuildImageCache();
    }

    /// <inheritdoc />
    public bool RemoveImage(string sourcePath)
    {
        if (manifest.Root is null || !imageCache.TryGetValue(sourcePath, out var cached))
        {
            return false;
        }

        // Build new content list without the removed entry
        var newContent = cached.Node.Content.Where(c => !ReferenceEquals(c, cached.Entry)).ToList();
        if (newContent.Count == cached.Node.Content.Count)
        {
            return false;
        }

        var newNode = cached.Node with { Content = newContent };
        var newRoot = ReplaceNodeInTree(manifest.Root, cached.Node, newNode);
        manifest = manifest with { Root = newRoot };

        // Rebuild cache
        RebuildImageCache();
        return true;
    }

    #endregion

    #region Metadata

    /// <inheritdoc />
    public string ScanConfigHash
    {
        get => manifest.Meta.ScanConfigHash;
        set => manifest = manifest with { Meta = manifest.Meta with { ScanConfigHash = value } };
    }

    /// <inheritdoc />
    public DateTime? LastScanned
    {
        get => manifest.Meta.LastScanned;
        set => manifest = manifest with { Meta = manifest.Meta with { LastScanned = value } };
    }

    /// <inheritdoc />
    public DateTime? LastImagesProcessed
    {
        get => manifest.Meta.LastImagesProcessed;
        set => manifest = manifest with { Meta = manifest.Meta with { LastImagesProcessed = value } };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, int> FormatQualities => manifest.Meta.FormatQualities;

    /// <inheritdoc />
    public void SetFormatQualities(IReadOnlyDictionary<string, int> qualities) =>
        manifest = manifest with { Meta = manifest.Meta with { FormatQualities = new Dictionary<string, int>(qualities) } };

    /// <inheritdoc />
    public string? GetProcessedFingerprint(string sourcePath) => processedImages.GetValueOrDefault(sourcePath);

    /// <inheritdoc />
    public void SetProcessedFingerprint(string sourcePath, string fingerprint) => processedImages[sourcePath] = fingerprint;

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await ReadAsync(GetManifestPath(projectEnvironment.Value.Path), logger, cancellationToken);
        manifest = loaded ?? new ImageManifest();
        RebuildImageCache();
        if (loaded is not null)
        {
            LogManifestLoaded(logger, imageCache.Count);
        }

        processedImages = new Dictionary<string, string>(manifest.Meta.ProcessedImages, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the manifest file path of a project.
    /// </summary>
    internal static string GetManifestPath(string projectPath) =>
        Path.Combine(projectPath, ProjectPaths.Cache, ManifestFileName);

    /// <summary>
    /// Reads a manifest file without side effects.
    /// </summary>
    /// <returns>
    /// The manifest, or <c>null</c> when the file is missing, unreadable or written
    /// by another manifest version.
    /// </returns>
    internal static async Task<ImageManifest?> ReadAsync(
        string manifestPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(manifestPath))
        {
            LogManifestNotFound(logger, manifestPath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var loaded = JsonSerializer.Deserialize(json, ManifestJsonContext.Default.ImageManifest);

            // Source-generated deserialization leaves Meta null when "_meta" is absent.
            if (loaded?.Meta is null)
            {
                LogManifestInvalid(logger, manifestPath);
                return null;
            }

            if (loaded.Meta.Version != ManifestMeta.CurrentVersion)
            {
                LogManifestOutdated(logger, manifestPath, loaded.Meta.Version, ManifestMeta.CurrentVersion);
                return null;
            }

            return WithDefaultMetaCollections(loaded);
        }
        catch (JsonException ex)
        {
            LogManifestParseError(logger, manifestPath, ex);
            return null;
        }
    }

    /// <summary>
    /// Enumerates every image in the tree with its manifest key and containing node.
    /// </summary>
    /// <remarks>
    /// Keys are source paths with forward slashes; an image listed on several pages
    /// (filtered images carry their original <see cref="ImageContent.SourcePath"/>)
    /// yields the same key each time.
    /// </remarks>
    internal static IEnumerable<(string SourcePath, ImageContent Image, ManifestEntry Node)> EnumerateImages(
        ManifestEntry root)
    {
        var pending = new Stack<ManifestEntry>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            foreach (var image in node.Content)
            {
                yield return (GetImageSourcePath(node.Path, image), image, node);
            }

            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                pending.Push(node.Children[i]);
            }
        }
    }

    /// <summary>
    /// Replaces meta collections that are missing from older manifest files with empty ones.
    /// </summary>
    /// <remarks>
    /// Source-generated System.Text.Json deserialization assigns <c>null</c> to init-only
    /// properties absent from the JSON instead of keeping their initializers.
    /// </remarks>
    private static ImageManifest WithDefaultMetaCollections(ImageManifest loaded) =>
        loaded with
        {
            Meta = loaded.Meta with
            {
                FormatQualities = loaded.Meta.FormatQualities ?? new Dictionary<string, int>(),
                ProcessedImages = loaded.Meta.ProcessedImages ?? new Dictionary<string, string>(),
            }
        };

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var manifestPath = GetManifestPath(projectEnvironment.Value.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        var tempPath = manifestPath + ".tmp";

        // Update timestamp and persist the processing state in stable key order
        manifest = manifest with
        {
            Meta = manifest.Meta with
            {
                LastUpdated = timeProvider.GetUtcNow().UtcDateTime,
                ProcessedImages = processedImages
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .ToDictionary(StringComparer.Ordinal)
            }
        };

        try
        {
            // Write to temp file first
            var json = JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.ImageManifest);
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
        processedImages.Clear();
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

        foreach (var stale in processedImages.Keys.Where(key => !existingSourcePaths.Contains(key)).ToList())
        {
            processedImages.Remove(stale);
        }

        if (orphans.Count > 0)
        {
            LogOrphansRemoved(logger, orphans.Count);
        }

        return orphans;
    }

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

        foreach (var (sourcePath, image, node) in EnumerateImages(manifest.Root))
        {
            imageCache[sourcePath] = (image, node);
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
    /// Replace a node in the tree with a new (immutable) version, returning a new tree root.
    /// </summary>
    /// <remarks>
    /// Uses reference equality to find the target node — works because the caller
    /// already located <paramref name="oldNode"/> via <see cref="FindNodeByPath"/>.
    /// All ancestors are rebuilt with <c>with</c>-expressions to maintain immutability.
    /// </remarks>
    private static ManifestEntry ReplaceNodeInTree(ManifestEntry root, ManifestEntry oldNode, ManifestEntry newNode)
    {
        if (ReferenceEquals(root, oldNode))
        {
            return newNode;
        }

        // Recurse into children, rebuilding the path to the changed node
        for (var i = 0; i < root.Children.Count; i++)
        {
            var child = root.Children[i];
            var newChild = ReplaceNodeInTree(child, oldNode, newNode);
            if (!ReferenceEquals(newChild, child))
            {
                var newChildren = root.Children.ToList();
                newChildren[i] = newChild;
                return root with { Children = newChildren };
            }
        }

        return root; // unchanged subtree
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
    private static string GetImageSourcePath(string nodePath, ImageContent content)
    {
        // If content has SourcePath set, use it directly (handles filtered images)
        if (!string.IsNullOrEmpty(content.SourcePath))
        {
            return content.SourcePath.Replace('\\', '/');
        }

        var path = string.IsNullOrEmpty(nodePath)
            ? content.Filename
            : $"{nodePath}/{content.Filename}";
        return path.Replace('\\', '/');
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Compute hash for scan configuration.
    /// </summary>
    /// <remarks>
    /// When this hash changes, all metadata needs to be re-read from source files.
    /// Includes: metadata version, placeholder strategy, min dimensions.
    /// </remarks>
    public static string ComputeScanConfigHash(
        PlaceholderStrategy placeholderStrategy,
        int minWidth,
        int minHeight)
    {
        var input = $"metadata:{NetVipsImageProcessor.MetadataVersion}|placeholder:{placeholderStrategy}|minWidth:{minWidth}|minHeight:{minHeight}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..12];
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Manifest not found at {Path}")]
    private static partial void LogManifestNotFound(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Manifest at {Path} is invalid and is ignored until the next scan")]
    private static partial void LogManifestInvalid(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Manifest at {Path} has version {Version} (current: {CurrentVersion}) and is ignored until the next scan")]
    private static partial void LogManifestOutdated(ILogger logger, string path, int version, int currentVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded manifest with {Count} images")]
    private static partial void LogManifestLoaded(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse manifest at {Path}; it is ignored until the next scan")]
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

/// <summary>
/// Source-generated JSON serializer context for the image manifest cache file.
/// </summary>
[JsonSerializable(typeof(ImageManifest))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
internal sealed partial class ManifestJsonContext : JsonSerializerContext;

