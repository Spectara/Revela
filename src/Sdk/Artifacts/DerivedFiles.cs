namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Removes derived files that a package wrote below a directory it owns
/// (typically <c>.cache/&lt;page&gt;/&lt;name&gt;.json</c>).
/// </summary>
/// <remarks>
/// Use it from <see cref="IArtifactInvalidator.InvalidateAsync"/> and from the matching
/// <c>clean</c> step so both remove exactly the same files. Symbolic links and junctions
/// are never followed, so a link inside the directory cannot make Revela delete files
/// elsewhere on disk.
/// </remarks>
public static class DerivedFiles
{
    /// <summary>
    /// Deletes every file named <paramref name="fileName"/> below <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">The root directory to search. A missing directory deletes nothing.</param>
    /// <param name="fileName">The exact file name to delete (for example <c>"calendar.json"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What was deleted and which files could not be deleted.</returns>
    public static DerivedFileDeletion DeleteAll(
        string directory,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!Directory.Exists(directory))
        {
            return new DerivedFileDeletion();
        }

        var deletedCount = 0;
        var deletedBytes = 0L;
        var failures = new List<DerivedFileDeletionFailure>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = MatchType.Simple,
            MatchCasing = MatchCasing.PlatformDefault,
        };

        foreach (var file in Directory.EnumerateFiles(directory, fileName, options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var length = new FileInfo(file).Length;
                File.Delete(file);
                deletedCount++;
                deletedBytes += length;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(new DerivedFileDeletionFailure(file, exception.Message));
            }
        }

        return new DerivedFileDeletion
        {
            DeletedCount = deletedCount,
            DeletedBytes = deletedBytes,
            Failures = failures,
        };
    }
}

/// <summary>
/// Result of <see cref="DerivedFiles.DeleteAll"/>.
/// </summary>
public sealed record DerivedFileDeletion
{
    /// <summary>Number of files deleted.</summary>
    public int DeletedCount { get; init; }

    /// <summary>Total size of the deleted files in bytes.</summary>
    public long DeletedBytes { get; init; }

    /// <summary>Files that could not be deleted; empty when everything was removed.</summary>
    public IReadOnlyList<DerivedFileDeletionFailure> Failures { get; init; } = [];
}

/// <summary>
/// A file that <see cref="DerivedFiles.DeleteAll"/> could not delete.
/// </summary>
/// <param name="Path">Full path of the file.</param>
/// <param name="Message">Plain-text reason from the file system.</param>
public sealed record DerivedFileDeletionFailure(string Path, string Message);
