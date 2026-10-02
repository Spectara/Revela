using System.Text.Json;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// One-time move from the legacy <c>.cache</c> folder to <see cref="ProjectPaths.Revela"/>.
/// </summary>
/// <remarks>
/// <para>
/// Before beta.21 everything lived in <c>.cache</c>: the scan manifest, plugin data and the image
/// processing state (first inside the manifest's <c>_meta.processedImages</c>, later in
/// <c>.cache/images.json</c>). Re-encoding a large AVIF library takes hours, so the state is
/// carried over to <see cref="ProjectPaths.State"/> first; then the remaining reproducible data
/// is moved to <see cref="ProjectPaths.Cache"/> so the next build needs no rescan.
/// </para>
/// <para>
/// When <see cref="ProjectPaths.Cache"/> already exists, the legacy folder is left in place (with
/// a warning) rather than merged or deleted. When the state cannot be carried over, nothing is
/// moved, so the next run tries again.
/// </para>
/// <para>
/// Runs before the manifest or the image state is read or written; a project without
/// <c>.cache</c> costs one directory check.
/// </para>
/// </remarks>
internal static partial class LegacyCacheCarryOver
{
    /// <summary>The cache folder of earlier versions, relative to the project.</summary>
    internal const string LegacyCacheDirectory = ".cache";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task RunAsync(string projectPath, ILogger logger, CancellationToken cancellationToken = default)
    {
        var legacyPath = Path.Combine(projectPath, LegacyCacheDirectory);
        if (!Directory.Exists(legacyPath))
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (Directory.Exists(legacyPath)
                && await TryCarryOverImageStateAsync(projectPath, legacyPath, logger, cancellationToken))
            {
                MoveCache(projectPath, legacyPath, logger);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<bool> TryCarryOverImageStateAsync(
        string projectPath,
        string legacyPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var statePath = ImageStateStore.GetStatePath(projectPath);
        var legacyStatePath = Path.Combine(legacyPath, ImageStateStore.FileName);
        try
        {
            if (File.Exists(statePath))
            {
                // The current state wins; a leftover legacy file would only confuse.
                File.Delete(legacyStatePath);
                return true;
            }

            if (File.Exists(legacyStatePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                File.Move(legacyStatePath, statePath);
                LogStateMoved(logger, legacyStatePath, statePath);
                return true;
            }

            await ImageStateStore.SeedFromManifestAsync(
                Path.Combine(legacyPath, ManifestService.ManifestFileName),
                statePath,
                logger,
                cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogStateCarryOverFailed(logger, legacyPath, ex);
            return false;
        }
    }

    private static void MoveCache(string projectPath, string legacyPath, ILogger logger)
    {
        var cachePath = Path.GetFullPath(Path.Combine(projectPath, ProjectPaths.Cache));
        if (Directory.Exists(cachePath))
        {
            LogLegacyCacheLeft(logger, legacyPath, cachePath);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            Directory.Move(legacyPath, cachePath);
            LogCacheMoved(logger, legacyPath, cachePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogCacheMoveFailed(logger, legacyPath, cachePath, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Moved the image processing state from {LegacyPath} to {Path}")]
    private static partial void LogStateMoved(ILogger logger, string legacyPath, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not carry over the image processing state from {LegacyPath}; it is left in place and tried again on the next run")]
    private static partial void LogStateCarryOverFailed(ILogger logger, string legacyPath, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Moved the cache from {LegacyPath} to {Path}")]
    private static partial void LogCacheMoved(ILogger logger, string legacyPath, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not move the cache from {LegacyPath} to {Path}; the next scan rebuilds it")]
    private static partial void LogCacheMoveFailed(ILogger logger, string legacyPath, string path, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The old cache folder {LegacyPath} is no longer used ({Path} exists). It holds only reproducible data and can be deleted")]
    private static partial void LogLegacyCacheLeft(ILogger logger, string legacyPath, string path);
}
