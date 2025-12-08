using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Manifest;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Service for managing the image manifest file.
/// </summary>
/// <remarks>
/// The manifest enables incremental builds by:
/// - Tracking source image hashes for change detection
/// - Storing generated sizes/formats for templates
/// - Caching EXIF data (eliminates need for separate ExifCache)
/// - Tracking config changes (forces full rebuild when sizes change)
///
/// Holds manifest state in memory, persists on SaveAsync.
/// Cache directory is determined from current working directory.
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

    #region Image Entries

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ImageManifestEntry> Images => manifest.Images;

    /// <inheritdoc />
    public ImageManifestEntry? GetImage(string sourcePath)
    {
        return manifest.Images.TryGetValue(sourcePath, out var entry) ? entry : null;
    }

    /// <inheritdoc />
    public void SetImage(string sourcePath, ImageManifestEntry entry)
    {
        manifest.Images[sourcePath] = entry;
    }

    /// <inheritdoc />
    public bool RemoveImage(string sourcePath)
    {
        return manifest.Images.Remove(sourcePath);
    }

    #endregion

    #region Gallery Entries

    /// <inheritdoc />
    public IReadOnlyList<GalleryManifestEntry> Galleries => manifest.Galleries;

    /// <inheritdoc />
    public void SetGalleries(IEnumerable<GalleryManifestEntry> galleries)
    {
        manifest.Galleries.Clear();
        manifest.Galleries.AddRange(galleries);
    }

    #endregion

    #region Navigation Entries

    /// <inheritdoc />
    public IReadOnlyList<NavigationManifestEntry> Navigation => manifest.Navigation;

    /// <inheritdoc />
    public void SetNavigation(IEnumerable<NavigationManifestEntry> navigation)
    {
        manifest.Navigation.Clear();
        manifest.Navigation.AddRange(navigation);
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
            }
            else
            {
                manifest = loaded;
                LogManifestLoaded(logger, manifest.Images.Count);
            }
        }
        catch (JsonException ex)
        {
            LogManifestParseError(logger, manifestPath, ex);
            manifest = new ImageManifest();
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

            LogManifestSaved(logger, manifest.Images.Count, manifestPath);
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
    }

    #endregion

    #region Utilities

    /// <inheritdoc />
    public IReadOnlyList<string> RemoveOrphans(IReadOnlySet<string> existingSourcePaths)
    {
        var orphans = manifest.Images.Keys
            .Where(key => !existingSourcePaths.Contains(key))
            .ToList();

        foreach (var orphan in orphans)
        {
            manifest.Images.Remove(orphan);
            LogOrphanRemoved(logger, orphan);
        }

        if (orphans.Count > 0)
        {
            LogOrphansRemoved(logger, orphans.Count);
        }

        return orphans;
    }

    private static string GetCacheDirectory()
    {
        return Path.Combine(Environment.CurrentDirectory, CacheDirectoryName);
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
        IReadOnlyList<string> formats,
        int quality)
    {
        var sizesStr = string.Join(",", sizes.OrderBy(s => s));
        var formatsStr = string.Join(",", formats.OrderBy(f => f));
        var input = $"sizes:{sizesStr}|formats:{formatsStr}|quality:{quality}";
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
    public static bool NeedsProcessing(ImageManifestEntry? existingEntry, string sourceHash)
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

    #endregion
}
