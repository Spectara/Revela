using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spectara.Revela.Commands.Generate.Models;

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
/// Inspired by expose.sh caching strategy.
/// </remarks>
public sealed partial class ImageManifestService(ILogger<ImageManifestService> logger)
{
    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Load manifest from cache directory.
    /// </summary>
    /// <param name="cacheDirectory">Path to .cache directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Existing manifest or new empty manifest</returns>
    public async Task<ImageManifest> LoadAsync(
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(cacheDirectory, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            LogManifestNotFound(logger, manifestPath);
            return new ImageManifest();
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<ImageManifest>(json, JsonOptions);

            if (manifest is null)
            {
                LogManifestInvalid(logger, manifestPath);
                return new ImageManifest();
            }

            LogManifestLoaded(logger, manifest.Images.Count);
            return manifest;
        }
        catch (JsonException ex)
        {
            LogManifestParseError(logger, manifestPath, ex);
            return new ImageManifest();
        }
    }

    /// <summary>
    /// Save manifest to cache directory.
    /// </summary>
    /// <remarks>
    /// Uses atomic write (temp file + rename) to prevent corruption.
    /// </remarks>
    public async Task SaveAsync(
        ImageManifest manifest,
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
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
    /// Check if an image needs to be processed.
    /// </summary>
    /// <param name="manifest">Current manifest</param>
    /// <param name="sourcePath">Relative path to source image</param>
    /// <param name="sourceHash">Hash of source image</param>
    /// <returns>True if image needs processing, false if unchanged</returns>
    public static bool NeedsProcessing(
        ImageManifest manifest,
        string sourcePath,
        string sourceHash)
    {
        if (!manifest.Images.TryGetValue(sourcePath, out var entry))
        {
            return true; // New image
        }

        return entry.Hash != sourceHash; // Changed if hash differs
    }

    /// <summary>
    /// Check if configuration has changed since last build.
    /// </summary>
    /// <returns>True if config changed (all images need regeneration)</returns>
    public static bool ConfigChanged(ImageManifest manifest, string currentConfigHash)
    {
        return manifest.Meta.ConfigHash != currentConfigHash;
    }

    /// <summary>
    /// Remove orphaned entries (source files that no longer exist).
    /// </summary>
    /// <param name="manifest">Manifest to clean</param>
    /// <param name="existingSourcePaths">Set of source paths that currently exist</param>
    /// <returns>List of removed entry keys</returns>
    public IReadOnlyList<string> RemoveOrphans(
        ImageManifest manifest,
        IReadOnlySet<string> existingSourcePaths)
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
