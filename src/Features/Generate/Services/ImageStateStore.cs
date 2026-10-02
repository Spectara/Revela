using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Image processing state: which source images have current variants, and what they were made from.
/// </summary>
/// <remarks>
/// <para>
/// Persisted in <c>.cache/images.json</c> with its own schema version, separate from the scan
/// manifest, so a manifest format change or rebuild never re-encodes images. Only things that
/// change image output invalidate an entry (see <see cref="ImageService"/>).
/// </para>
/// <para>
/// Thread-safe for <see cref="Get"/>, <see cref="Set"/> and <see cref="RemoveExcept"/>.
/// Callers serialize <see cref="SaveAsync"/>.
/// </para>
/// </remarks>
internal sealed partial class ImageStateStore(
    IOptions<ProjectEnvironment> projectEnvironment,
    ILogger<ImageStateStore> logger)
{
    /// <summary>The state schema version this Revela reads and writes.</summary>
    internal const int CurrentVersion = 1;

    private const string FileName = "images.json";

    private readonly Lock stateLock = new();
    private Dictionary<string, ProcessedImage> images = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads the state of the current project, replacing what is held in memory.
    /// </summary>
    /// <remarks>
    /// A missing state is first carried over from a manifest written by beta.21.
    /// A missing, unreadable or other-version file yields an empty state.
    /// </remarks>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var projectPath = projectEnvironment.Value.Path;
        await SeedFromLegacyManifestAsync(projectPath, logger, cancellationToken);
        var loaded = await ReadAsync(GetStatePath(projectPath), logger, cancellationToken);

        lock (stateLock)
        {
            images = loaded;
        }
    }

    /// <summary>
    /// Gets the recorded state of an image, or <c>null</c> when its variants were never recorded.
    /// </summary>
    /// <param name="sourcePath">Source path relative to the source directory, forward slashes.</param>
    public ProcessedImage? Get(string sourcePath)
    {
        lock (stateLock)
        {
            return images.GetValueOrDefault(sourcePath);
        }
    }

    /// <summary>
    /// Records an image after all of its variants were written.
    /// </summary>
    /// <param name="sourcePath">Source path relative to the source directory, forward slashes.</param>
    /// <param name="image">What the variants were made from.</param>
    public void Set(string sourcePath, ProcessedImage image)
    {
        lock (stateLock)
        {
            images[sourcePath] = image;
        }
    }

    /// <summary>
    /// Forgets images whose source is no longer part of the site.
    /// </summary>
    public void RemoveExcept(IReadOnlySet<string> sourcePaths)
    {
        lock (stateLock)
        {
            foreach (var stale in images.Keys.Where(key => !sourcePaths.Contains(key)).ToList())
            {
                images.Remove(stale);
            }
        }
    }

    /// <summary>
    /// Writes the state atomically (temporary file, then replace).
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        List<KeyValuePair<string, ProcessedImage>> snapshot;
        lock (stateLock)
        {
            snapshot = [.. images];
        }

        await WriteAsync(GetStatePath(projectEnvironment.Value.Path), snapshot, cancellationToken);
    }

    /// <summary>
    /// One-time carry-over of the processing state that beta.21 kept in the manifest
    /// (<c>_meta.processedImages</c> and <c>_meta.formatQualities</c>).
    /// </summary>
    /// <remarks>
    /// Runs only while no state file exists, so it must happen before anything rewrites the
    /// manifest without those fields; <see cref="ManifestService.SaveAsync"/> calls it first.
    /// The manifest version is deliberately not checked: a scan may already have discarded the
    /// manifest's tree, and each fingerprint carries the pipeline version it is valid for.
    /// Best effort: on failure the images are simply processed again.
    /// </remarks>
    internal static async Task SeedFromLegacyManifestAsync(
        string projectPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var statePath = GetStatePath(projectPath);
        var manifestPath = ManifestService.GetManifestPath(projectPath);
        if (File.Exists(statePath) || !File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            var seeded = await ReadLegacyStateAsync(manifestPath, cancellationToken);
            if (seeded.Count == 0)
            {
                return;
            }

            await WriteAsync(statePath, seeded, cancellationToken);
            LogStateSeeded(logger, seeded.Count, statePath);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogSeedFailed(logger, manifestPath, ex);
        }
    }

    private static string GetStatePath(string projectPath) =>
        Path.Combine(projectPath, ProjectPaths.Cache, FileName);

    private static async Task<List<KeyValuePair<string, ProcessedImage>>> ReadLegacyStateAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(manifestPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind is not JsonValueKind.Object
            || !document.RootElement.TryGetProperty("_meta", out var meta)
            || meta.ValueKind is not JsonValueKind.Object
            || !meta.TryGetProperty("processedImages", out var processed)
            || processed.ValueKind is not JsonValueKind.Object)
        {
            return [];
        }

        var qualities = new Dictionary<string, int>(StringComparer.Ordinal);
        if (meta.TryGetProperty("formatQualities", out var formatQualities)
            && formatQualities.ValueKind is JsonValueKind.Object)
        {
            foreach (var format in formatQualities.EnumerateObject())
            {
                if (format.Value.TryGetInt32(out var quality))
                {
                    qualities[format.Name] = quality;
                }
            }
        }

        var seeded = new List<KeyValuePair<string, ProcessedImage>>();
        foreach (var image in processed.EnumerateObject())
        {
            if (image.Value.ValueKind is JsonValueKind.String && image.Value.GetString() is { Length: > 0 } fingerprint)
            {
                seeded.Add(new(image.Name, new ProcessedImage { Fingerprint = fingerprint, Qualities = qualities }));
            }
        }

        return seeded;
    }

    private static async Task<Dictionary<string, ProcessedImage>> ReadAsync(
        string statePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var state = new Dictionary<string, ProcessedImage>(StringComparer.Ordinal);
        if (!File.Exists(statePath))
        {
            return state;
        }

        try
        {
            await using var stream = File.OpenRead(statePath);
            var document = await JsonSerializer.DeserializeAsync(
                stream,
                ImageStateJsonContext.Default.ImageStateDocument,
                cancellationToken);

            if (document?.Version != CurrentVersion)
            {
                LogStateVersionIgnored(logger, statePath, document?.Version ?? 0, CurrentVersion);
                return state;
            }

            foreach (var (sourcePath, image) in document.Images ?? new Dictionary<string, ProcessedImage?>())
            {
                if (image is { Fingerprint.Length: > 0 })
                {
                    // Source-generated deserialization leaves an absent "qualities" null.
                    state[sourcePath] = image with { Qualities = image.Qualities ?? new Dictionary<string, int>() };
                }
            }

            return state;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogStateUnreadable(logger, statePath, ex);
            return state;
        }
    }

    private static async Task WriteAsync(
        string statePath,
        IEnumerable<KeyValuePair<string, ProcessedImage>> entries,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        var document = new ImageStateDocument
        {
            Version = CurrentVersion,
            Images = entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, ProcessedImage? (entry) => entry.Value, StringComparer.Ordinal)
        };

        var tempPath = statePath + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, document, ImageStateJsonContext.Default.ImageStateDocument, cancellationToken);
            }

            await AtomicFileReplace.ReplaceAsync(tempPath, statePath, cancellationToken);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The leftover temporary file is overwritten by the next save.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Carried over the processing state of {Count} images from the manifest to {Path}")]
    private static partial void LogStateSeeded(ILogger logger, int count, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not carry over the image processing state from {Path}; images will be processed again")]
    private static partial void LogSeedFailed(ILogger logger, string path, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Image state at {Path} has version {Version} (current: {CurrentVersion}); all images will be processed again")]
    private static partial void LogStateVersionIgnored(ILogger logger, string path, int version, int currentVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image state at {Path} is unreadable; all images will be processed again")]
    private static partial void LogStateUnreadable(ILogger logger, string path, Exception exception);
}

/// <summary>
/// Source-generated JSON serializer context for the image processing state file.
/// </summary>
[JsonSerializable(typeof(ImageStateDocument))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
internal sealed partial class ImageStateJsonContext : JsonSerializerContext;
