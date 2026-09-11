using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectara.Revela.Plugins.Compress.Services;

internal sealed partial class CompressedSiteOwnership : IDisposable
{
    private const string ManifestName = ".revela-compress.manifest";
    private const string Owner = "Spectara.Revela.Plugins.Compress";
    private static readonly SemaphoreSlim OperationGate = new(1, 1);
    private static readonly OwnershipJsonContext JsonContext = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    });
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    private readonly string root;
    private Manifest manifest = new() { Owner = Owner, Version = 1, Files = [] };
    private byte[]? savedManifest;

    private CompressedSiteOwnership(string outputPath) => root = Path.GetFullPath(outputPath);

    public static async Task<CompressedSiteOwnership> OpenAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var ownership = new CompressedSiteOwnership(outputPath);
        await OperationGate.WaitAsync(cancellationToken);
        try
        {
            var manifestPath = ownership.SafePath(ManifestName);
            if (File.Exists(manifestPath))
            {
                ownership.savedManifest = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
                try
                {
                    ownership.manifest = JsonSerializer.Deserialize(
                        ownership.savedManifest, JsonContext.Manifest)
                        ?? throw new IOException("Invalid compression ownership manifest.");
                }
                catch (JsonException exception)
                {
                    throw new IOException("Invalid compression ownership manifest.", exception);
                }
            }

            ownership.Validate();
            return ownership;
        }
        catch
        {
            ownership.Dispose();
            throw;
        }
    }

    public string GetSourcePath(string filePath) => SafePath(RelativePath(filePath));

    public async Task<CompressionStats> CleanAsync(CancellationToken cancellationToken = default)
    {
        var stats = new CompressionStats();
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            Validate();
            foreach (var entry in manifest.Files.ToArray())
            {
                var existed = await RemoveAsync(entry.Path, cancellationToken);
                if (existed)
                {
                    var format = entry.Path.EndsWith(".gz", StringComparison.Ordinal) ? stats.Gzip : stats.Brotli;
                    format.FileCount++;
                    format.CompressedSize += entry.Length;
                }
            }
        }
        finally
        {
            mutationGate.Release();
        }

        return stats;
    }

    public async Task RemoveSidecarsAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            var relativePath = RelativePath(sourcePath);
            await RemoveAsync(relativePath + ".gz", cancellationToken);
            await RemoveAsync(relativePath + ".br", cancellationToken);
        }
        finally
        {
            mutationGate.Release();
        }
    }

    public async Task<long> PublishAsync(
        string destination,
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

            var entry = await FingerprintAsync(temporaryPath, cancellationToken);
            entry = entry with { Path = relativePath };
            await mutationGate.WaitAsync(cancellationToken);
            try
            {
                await EnsureManifestUnchangedAsync(cancellationToken);
                await RemoveAsync(relativePath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(SafePath(temporaryPath), SafePath(relativePath));
                try
                {
                    await SaveAsync(manifest with { Files = [.. manifest.Files, entry] }, cancellationToken);
                }
                catch (Exception exception)
                {
                    try
                    {
                        if (await MatchesAsync(entry, CancellationToken.None))
                        {
                            File.Delete(SafePath(relativePath));
                        }
                    }
                    catch (Exception cleanupException)
                    {
                        RecordCleanupFailure(exception, "Publish rollback", cleanupException);
                    }
                    throw;
                }
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
                DeleteTemporaryFile(temporaryPath, primaryException, "Publish staging cleanup");
            }
        }
    }

    public void Dispose()
    {
        mutationGate.Dispose();
        OperationGate.Release();
    }

    private void Validate()
    {
        if (manifest.Owner != Owner || manifest.Version != 1 || manifest.Files is null)
        {
            throw new IOException("Foreign or unsupported compression ownership manifest.");
        }

        var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var entry in manifest.Files)
        {
            if (entry is null || string.IsNullOrEmpty(entry.Path) || entry.Length < 0 ||
                entry.Sha256 is null || entry.Sha256.Length != 64 || !entry.Sha256.All(char.IsAsciiHexDigit) ||
                !(entry.Path.EndsWith(".gz", StringComparison.Ordinal) || entry.Path.EndsWith(".br", StringComparison.Ordinal)) ||
                !paths.Add(entry.Path))
            {
                throw new IOException("Invalid compression ownership entry.");
            }
            _ = SafePath(entry.Path);
        }
    }

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

    private async Task<bool> RemoveAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = manifest.Files.FirstOrDefault(candidate => string.Equals(
            candidate.Path, relativePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        var path = SafePath(relativePath);
        var exists = File.Exists(path);
        if (entry is null)
        {
            if (exists)
            {
                throw new IOException($"Unowned compression destination: '{relativePath}'.");
            }
            return false;
        }

        await EnsureManifestUnchangedAsync(cancellationToken);
        if (exists)
        {
            if (!await MatchesAsync(entry, cancellationToken))
            {
                throw new IOException($"Owned compressed file was changed: '{relativePath}'.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(SafePath(relativePath));
        }

        await SaveAsync(manifest with { Files = [.. manifest.Files.Where(candidate => candidate != entry)] }, cancellationToken);
        return exists;
    }

    private async Task EnsureManifestUnchangedAsync(CancellationToken cancellationToken)
    {
        var path = SafePath(ManifestName);
        if (savedManifest is null)
        {
            if (File.Exists(path))
            {
                throw new IOException("Compression ownership manifest appeared during the operation.");
            }
        }
        else if (!File.Exists(path) || !(await File.ReadAllBytesAsync(path, cancellationToken)).AsSpan().SequenceEqual(savedManifest))
        {
            throw new IOException("Compression ownership manifest changed during the operation.");
        }
    }

    private async Task SaveAsync(Manifest replacement, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(replacement, JsonContext.Manifest);
        var temporaryPath = ManifestName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var created = false;
        Exception? primaryException = null;
        try
        {
            await using (var stream = new FileStream(SafePath(temporaryPath), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            await EnsureManifestUnchangedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(SafePath(temporaryPath), SafePath(ManifestName), overwrite: savedManifest is not null);
            savedManifest = bytes;
            manifest = replacement;
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
                DeleteTemporaryFile(temporaryPath, primaryException, "Manifest staging cleanup");
            }
        }
    }

    private void DeleteTemporaryFile(string relativePath, Exception? primaryException, string operation)
    {
        try
        {
            File.Delete(SafePath(relativePath));
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
    }

    [JsonSerializable(typeof(Manifest))]
    private sealed partial class OwnershipJsonContext : JsonSerializerContext;
}
