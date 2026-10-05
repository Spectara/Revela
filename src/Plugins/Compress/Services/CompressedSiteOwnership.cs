using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Plugins.Compress.Services;

/// <summary>
/// Length and SHA-256 of a source file, stored with each sidecar compressed from it.
/// </summary>
internal readonly record struct SourceFingerprint(long Length, string Sha256)
{
    public static SourceFingerprint FromContent(ReadOnlySpan<byte> content) =>
        new(content.Length, Convert.ToHexString(SHA256.HashData(content)));
}

/// <summary>
/// Records which <c>.gz</c>/<c>.br</c> sidecars in the output were created by this plugin, so
/// only those are ever replaced or deleted.
/// </summary>
/// <remarks>
/// <para>
/// The record lives in the plugin's folder, <c>.revela/compress/ownership.json</c>, not in the
/// output, so it is never published. It is part of <see cref="CompressArtifacts.PrecompressedSite"/>
/// (an output artifact): removed together with the sidecars it lists.
/// </para>
/// <para>
/// The record is loaded once and kept in memory. Changes are written back in batches by
/// <see cref="CommitAsync"/>: after <see cref="MinimumCheckpointInterval"/> changes or a quarter of
/// the record, whichever is larger, so the total record I/O of a run stays linear. Callers must
/// commit when done, and should call <see cref="CommitAfterFailureAsync"/> when an operation fails,
/// so that sidecars already moved into the output stay owned. Each commit first checks that the
/// record on disk is still the one this instance wrote; if the commit fails, the sidecars published
/// since the last commit are deleted again and the instance refuses further changes.
/// </para>
/// <para>
/// Publishing and removing sidecars may run concurrently as long as each call targets different
/// sidecars; only the in-memory record and the commits are serialized.
/// </para>
/// </remarks>
internal sealed partial class CompressedSiteOwnership : IDisposable
{
    /// <summary>File name of the record in the plugin's folder.</summary>
    internal const string RecordFileName = "ownership.json";
    private const string Owner = "Spectara.Revela.Plugins.Compress";
    private const int MinimumCheckpointInterval = 256;
    private static readonly SemaphoreSlim OperationGate = new(1, 1);
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly OwnershipJsonContext JsonContext = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    });
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    private readonly string root;
    private readonly string recordPath;
    private readonly Dictionary<string, OwnedFile> entries = new(PathComparer);
    private readonly Dictionary<string, OwnedFile> uncommitted = new(PathComparer);
    private int pendingChanges;
    private bool faulted;
    private byte[]? savedManifest;

    private CompressedSiteOwnership(string outputPath, string ownerDirectory)
    {
        root = Path.GetFullPath(outputPath);
        recordPath = Path.Combine(Path.GetFullPath(ownerDirectory), RecordFileName);
    }

    /// <summary>Gets how often the record was written.</summary>
    internal int RecordWrites { get; private set; }

    /// <summary>Gets how often the record was re-read to verify it is unchanged (the initial load excluded).</summary>
    internal int RecordReads { get; private set; }

    /// <summary>Gets the plugin's folder, <c>.revela/compress</c>, of a project.</summary>
    internal static string GetOwnerDirectory(string projectPath) =>
        Path.Combine(projectPath, ProjectPaths.GetOwnerDirectory(CompressArtifacts.PrecompressedSite.Owner));

    /// <summary>
    /// Opens the ownership record of an output directory.
    /// </summary>
    /// <param name="outputPath">The output directory holding the sidecars.</param>
    /// <param name="ownerDirectory">The plugin's folder (<see cref="GetOwnerDirectory"/>) holding the record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<CompressedSiteOwnership> OpenAsync(
        string outputPath,
        string ownerDirectory,
        CancellationToken cancellationToken = default)
    {
        var ownership = new CompressedSiteOwnership(outputPath, ownerDirectory);
        await OperationGate.WaitAsync(cancellationToken);
        try
        {
            var path = ownership.RecordPath();
            var manifest = new Manifest { Owner = Owner, Version = 1, Files = [] };
            if (File.Exists(path))
            {
                ownership.savedManifest = await File.ReadAllBytesAsync(path, cancellationToken);
                manifest = Parse(ownership.savedManifest);
            }

            ownership.Load(manifest);
            return ownership;
        }
        catch
        {
            ownership.Dispose();
            throw;
        }
    }

    public string GetSourcePath(string filePath) => SafePath(RelativePath(filePath));

    /// <summary>
    /// Returns the length of an owned sidecar that can be kept as it is: it was compressed from a
    /// source with the given fingerprint and still has its own recorded fingerprint.
    /// </summary>
    /// <returns>The sidecar's length, or <see langword="null"/> when it must be compressed again.</returns>
    public async Task<long?> GetReusableLengthAsync(
        string destination,
        SourceFingerprint source,
        CancellationToken cancellationToken = default)
    {
        var relativePath = RelativePath(destination);
        var entry = await FindAsync(relativePath, cancellationToken);
        if (entry is null || entry.SourceLength != source.Length ||
            !string.Equals(entry.SourceSha256, source.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await MatchesAsync(entry, cancellationToken) ? entry.Length : null;
    }

    /// <summary>
    /// Removes every owned sidecar. All existing sidecars are verified before the first is deleted.
    /// </summary>
    public async Task<CompressionStats> CleanAsync(CancellationToken cancellationToken = default)
    {
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfFaulted();
            var stats = await RemoveVerifiedAsync([.. entries.Values], cancellationToken);
            await CommitCoreAsync(cancellationToken);
            return stats;
        }
        catch (Exception exception)
        {
            await CommitAfterFailureCoreAsync(exception);
            throw;
        }
        finally
        {
            mutationGate.Release();
        }
    }

    /// <summary>
    /// Removes the owned sidecars whose source is not among <paramref name="sourcePaths"/>. All of
    /// them are verified before the first is deleted.
    /// </summary>
    public async Task RemoveOrphansAsync(IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default)
    {
        var sources = new HashSet<string>(sourcePaths.Select(RelativePath), PathComparer);
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfFaulted();
            await RemoveVerifiedAsync(
                [.. entries.Values.Where(entry => !sources.Contains(entry.Path[..^".gz".Length]))],
                cancellationToken);
            await CheckpointAsync(cancellationToken);
        }
        finally
        {
            mutationGate.Release();
        }
    }

    /// <summary>
    /// Deletes the record after <see cref="CleanAsync"/> removed every owned file; the next
    /// compression starts a new one.
    /// </summary>
    /// <exception cref="IOException">The record still owns files or changed during the operation.</exception>
    public async Task DeleteEmptyRecordAsync(CancellationToken cancellationToken = default)
    {
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfFaulted();
            if (entries.Count != 0)
            {
                throw new IOException("Compression ownership record still owns files.");
            }

            await EnsureManifestUnchangedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (savedManifest is not null)
            {
                File.Delete(RecordPath());
                savedManifest = null;
            }

            pendingChanges = 0;
        }
        finally
        {
            mutationGate.Release();
        }
    }

    public async Task RemoveSidecarsAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var relativePath = RelativePath(sourcePath);
        foreach (var sidecar in (string[])[relativePath + ".gz", relativePath + ".br"])
        {
            var entry = await FindAsync(sidecar, cancellationToken);
            await DeleteExistingAsync(sidecar, entry, cancellationToken);
            if (entry is not null)
            {
                await mutationGate.WaitAsync(CancellationToken.None);
                try
                {
                    Forget(entry);
                    await CheckpointAsync(CancellationToken.None);
                }
                finally
                {
                    mutationGate.Release();
                }
            }
        }
    }

    public Task<long> PublishAsync(
        string destination,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default) =>
        PublishAsync(destination, source: null, writeAsync, cancellationToken);

    /// <summary>
    /// Writes a sidecar to a staging file and moves it into place, replacing the owned sidecar
    /// of the same name. The record is updated at the next <see cref="CommitAsync"/>.
    /// </summary>
    /// <param name="destination">The sidecar to create or replace.</param>
    /// <param name="source">Fingerprint of the source the sidecar is compressed from.</param>
    /// <param name="writeAsync">Writes the sidecar's content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sidecar's length.</returns>
    public async Task<long> PublishAsync(
        string destination,
        SourceFingerprint? source,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default)
    {
        var relativePath = RelativePath(destination);
        var temporaryPath = relativePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var created = false;
        Exception? primaryException = null;
        try
        {
            await using (var stream = new FileStream(SafePath(temporaryPath), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                await writeAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            var entry = await FingerprintAsync(temporaryPath, cancellationToken) with
            {
                Path = relativePath,
                SourceLength = source?.Length,
                SourceSha256 = source?.Sha256
            };
            await DeleteExistingAsync(relativePath, await FindAsync(relativePath, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(SafePath(temporaryPath), SafePath(relativePath));

            // The sidecar is in the output now: register it even if cancellation was requested.
            await mutationGate.WaitAsync(CancellationToken.None);
            try
            {
                if (faulted)
                {
                    var faultedException = FaultedException();
                    await DeleteIfUnchangedAsync(entry, faultedException);
                    throw faultedException;
                }

                entries[relativePath] = entry;
                uncommitted[relativePath] = entry;
                pendingChanges++;
                await CheckpointAsync(CancellationToken.None);
            }
            finally
            {
                mutationGate.Release();
            }

            return entry.Length;
        }
        catch (Exception exception)
        {
            primaryException = exception;
            throw;
        }
        finally
        {
            if (created)
            {
                DeleteTemporaryFile(() => SafePath(temporaryPath), primaryException, "Publish staging cleanup");
            }
        }
    }

    /// <summary>
    /// Writes pending changes to the record. Once started, a commit is not cancelled.
    /// </summary>
    /// <exception cref="IOException">The record changed on disk since it was loaded or last written.</exception>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfFaulted();
            await CommitCoreAsync(cancellationToken);
        }
        finally
        {
            mutationGate.Release();
        }
    }

    /// <summary>
    /// Records the changes made before <paramref name="primaryException"/> so that sidecars already
    /// moved into the output stay owned. A failure to do so is attached to the primary exception.
    /// </summary>
    public async Task CommitAfterFailureAsync(Exception primaryException)
    {
        await mutationGate.WaitAsync(CancellationToken.None);
        try
        {
            await CommitAfterFailureCoreAsync(primaryException);
        }
        finally
        {
            mutationGate.Release();
        }
    }

    public void Dispose()
    {
        mutationGate.Dispose();
        OperationGate.Release();
    }

    private static Manifest Parse(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize(bytes, JsonContext.Manifest)
                ?? throw new IOException("Invalid compression ownership manifest.");
        }
        catch (JsonException exception)
        {
            throw new IOException("Invalid compression ownership manifest.", exception);
        }
    }

    private string RecordPath()
    {
        try
        {
            if ((File.GetAttributes(recordPath) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            {
                throw new IOException("Compression ownership record is linked or is not a file.");
            }
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }

        return recordPath;
    }

    private void Load(Manifest manifest)
    {
        if (manifest.Owner != Owner || manifest.Version != 1 || manifest.Files is null)
        {
            throw new IOException("Foreign or unsupported compression ownership manifest.");
        }

        foreach (var entry in manifest.Files)
        {
            if (entry is null || string.IsNullOrEmpty(entry.Path) || entry.Length < 0 || !IsSha256(entry.Sha256) ||
                (entry.SourceLength is null) != (entry.SourceSha256 is null) ||
                entry.SourceLength < 0 || (entry.SourceSha256 is not null && !IsSha256(entry.SourceSha256)) ||
                !(entry.Path.EndsWith(".gz", StringComparison.Ordinal) || entry.Path.EndsWith(".br", StringComparison.Ordinal)) ||
                !entries.TryAdd(entry.Path, entry))
            {
                throw new IOException("Invalid compression ownership entry.");
            }
            _ = SafePath(entry.Path);
        }
    }

    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private void ThrowIfFaulted()
    {
        if (faulted)
        {
            throw FaultedException();
        }
    }

    private static IOException FaultedException() =>
        new("Compression ownership record could not be saved; no further changes are made.");

    private string RelativePath(string filePath) => Path.GetRelativePath(root, Path.GetFullPath(filePath)).Replace('\\', '/');

    private string SafePath(string relativePath)
    {
        var components = relativePath.Split('/');
        if (Path.IsPathRooted(relativePath) || components.Any(component =>
            string.IsNullOrEmpty(component) || component is "." or ".." ||
            component.EndsWith('.') || component.EndsWith(' ') ||
            component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            component.Contains('\\', StringComparison.Ordinal) || component.Contains(':', StringComparison.Ordinal)))
        {
            throw new IOException("Unsafe compression ownership path.");
        }

        var path = root;
        foreach (var component in components)
        {
            path = Path.Combine(path, component);
            try
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    (path == Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)) &&
                     (attributes & FileAttributes.Directory) != 0))
                {
                    throw new IOException("Compression ownership path is linked or is not a file.");
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }

        return path;
    }

    private async Task<OwnedFile> FingerprintAsync(string relativePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(SafePath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);
        var length = stream.Length;
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return new OwnedFile { Path = relativePath, Length = length, Sha256 = Convert.ToHexString(hash) };
    }

    private async Task<bool> MatchesAsync(OwnedFile entry, CancellationToken cancellationToken)
    {
        if (!File.Exists(SafePath(entry.Path)))
        {
            return false;
        }

        var actual = await FingerprintAsync(entry.Path, cancellationToken);
        return actual.Length == entry.Length && string.Equals(actual.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<OwnedFile?> FindAsync(string relativePath, CancellationToken cancellationToken)
    {
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfFaulted();
            return entries.GetValueOrDefault(relativePath);
        }
        finally
        {
            mutationGate.Release();
        }
    }

    /// <summary>
    /// Deletes the file at <paramref name="relativePath"/> if it is the unchanged owned
    /// <paramref name="entry"/>; fails for an unowned or changed file. Concurrent callers use different paths.
    /// </summary>
    private async Task DeleteExistingAsync(string relativePath, OwnedFile? entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = SafePath(relativePath);
        if (!File.Exists(path))
        {
            return;
        }

        if (entry is null)
        {
            throw new IOException($"Unowned compression destination: '{relativePath}'.");
        }

        if (!await MatchesAsync(entry, cancellationToken))
        {
            throw new IOException($"Owned compressed file was changed: '{relativePath}'.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(path);
    }

    private async Task DeleteIfUnchangedAsync(OwnedFile entry, Exception primaryException)
    {
        try
        {
            if (await MatchesAsync(entry, CancellationToken.None))
            {
                File.Delete(SafePath(entry.Path));
            }
        }
        catch (Exception cleanupException)
        {
            RecordCleanupFailure(primaryException, "Publish rollback", cleanupException);
        }
    }

    /// <summary>
    /// Verifies all existing <paramref name="targets"/> before deleting any of them. Caller holds the gate.
    /// </summary>
    private async Task<CompressionStats> RemoveVerifiedAsync(IReadOnlyList<OwnedFile> targets, CancellationToken cancellationToken)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };
        var existing = new ConcurrentDictionary<OwnedFile, bool>();
        await Parallel.ForEachAsync(targets, options, async (target, token) =>
        {
            if (File.Exists(SafePath(target.Path)))
            {
                if (!await MatchesAsync(target, token))
                {
                    throw new IOException($"Owned compressed file was changed: '{target.Path}'.");
                }
                existing[target] = true;
            }
        });

        var deleted = new ConcurrentBag<OwnedFile>();
        try
        {
            await Parallel.ForEachAsync(existing.Keys, options, (target, token) =>
            {
                token.ThrowIfCancellationRequested();
                File.Delete(SafePath(target.Path));
                deleted.Add(target);
                return ValueTask.CompletedTask;
            });
        }
        finally
        {
            // Forget what is gone; files that could not be deleted stay owned.
            foreach (var target in targets.Where(target => !existing.ContainsKey(target)).Concat(deleted))
            {
                Forget(target);
            }
        }

        var stats = new CompressionStats();
        foreach (var target in deleted)
        {
            var format = target.Path.EndsWith(".gz", StringComparison.Ordinal) ? stats.Gzip : stats.Brotli;
            format.FileCount++;
            format.CompressedSize += target.Length;
        }

        return stats;
    }

    private void Forget(OwnedFile entry)
    {
        entries.Remove(entry.Path);
        uncommitted.Remove(entry.Path);
        pendingChanges++;
    }

    /// <summary>Commits once enough changes are pending. Caller holds the gate.</summary>
    private async Task CheckpointAsync(CancellationToken cancellationToken)
    {
        if (!faulted && pendingChanges >= Math.Max(MinimumCheckpointInterval, entries.Count / 4))
        {
            await CommitCoreAsync(cancellationToken);
        }
    }

    /// <summary>Writes pending changes; on failure deletes the uncommitted sidecars again. Caller holds the gate.</summary>
    private async Task CommitCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pendingChanges == 0)
        {
            return;
        }

        var replacement = new Manifest
        {
            Owner = Owner,
            Version = 1,
            Files = [.. entries.Values.OrderBy(entry => entry.Path, StringComparer.Ordinal)]
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(replacement, JsonContext.Manifest);
        try
        {
            await EnsureManifestUnchangedAsync(CancellationToken.None);
            await WriteRecordAsync(bytes, overwrite: savedManifest is not null, CancellationToken.None);
        }
        catch (Exception exception)
        {
            faulted = true;
            await RollBackUncommittedAsync(exception);
            throw;
        }

        savedManifest = bytes;
        uncommitted.Clear();
        pendingChanges = 0;
    }

    private async Task CommitAfterFailureCoreAsync(Exception primaryException)
    {
        if (faulted)
        {
            return;
        }

        try
        {
            await CommitCoreAsync(CancellationToken.None);
        }
        catch (Exception commitException)
        {
            RecordCleanupFailure(primaryException, "Ownership record commit", commitException);
        }
    }

    private async Task RollBackUncommittedAsync(Exception primaryException)
    {
        foreach (var entry in uncommitted.Values)
        {
            await DeleteIfUnchangedAsync(entry, primaryException);
        }

        uncommitted.Clear();
    }

    private async Task EnsureManifestUnchangedAsync(CancellationToken cancellationToken)
    {
        var path = RecordPath();
        if (savedManifest is null)
        {
            if (File.Exists(path))
            {
                throw new IOException("Compression ownership manifest appeared during the operation.");
            }
            return;
        }

        RecordReads++;
        if (!File.Exists(path) || !(await File.ReadAllBytesAsync(path, cancellationToken)).AsSpan().SequenceEqual(savedManifest))
        {
            throw new IOException("Compression ownership manifest changed during the operation.");
        }
    }

    private async Task WriteRecordAsync(byte[] bytes, bool overwrite, CancellationToken cancellationToken)
    {
        RecordWrites++;
        var path = RecordPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var created = false;
        Exception? primaryException = null;
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, RecordPath(), overwrite);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            throw;
        }
        finally
        {
            if (created)
            {
                DeleteTemporaryFile(() => temporaryPath, primaryException, "Manifest staging cleanup");
            }
        }
    }

    private static void DeleteTemporaryFile(Func<string> resolvePath, Exception? primaryException, string operation)
    {
        try
        {
            File.Delete(resolvePath());
        }
        catch (Exception cleanupException) when (primaryException is not null)
        {
            RecordCleanupFailure(primaryException, operation, cleanupException);
        }
    }

    private static void RecordCleanupFailure(Exception primaryException, string operation, Exception cleanupException)
    {
        try
        {
            var key = Owner + ".CleanupFailures";
            var failures = primaryException.Data[key] as string[] ?? [];
            var failure = string.Create(CultureInfo.InvariantCulture,
                $"{operation}: {cleanupException.GetType().Name} (0x{cleanupException.HResult:X8})");
            primaryException.Data[key] = (string[])[.. failures, failure];
        }
        catch (Exception)
        {
        }
    }

    private sealed record Manifest
    {
        public required string Owner { get; init; }
        public required int Version { get; init; }
        public required IReadOnlyList<OwnedFile> Files { get; init; }
    }

    private sealed record OwnedFile
    {
        public required string Path { get; init; }
        public required long Length { get; init; }
        public required string Sha256 { get; init; }

        /// <summary>Length of the source the sidecar was compressed from; absent in older records.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? SourceLength { get; init; }

        /// <summary>SHA-256 of the source the sidecar was compressed from; absent in older records.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SourceSha256 { get; init; }
    }

    [JsonSerializable(typeof(Manifest))]
    private sealed partial class OwnershipJsonContext : JsonSerializerContext;
}
