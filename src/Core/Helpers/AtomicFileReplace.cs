namespace Spectara.Revela.Core.Helpers;

/// <summary>
/// Moves a fully written temporary file over its target, tolerating short-lived readers.
/// </summary>
/// <remarks>
/// On Windows a replace fails while the target is open without <see cref="FileShare.Delete"/>, which is
/// how the configuration file watcher's reload and virus scanners open it. Those handles are short-lived,
/// so the replace is retried for about a second before the error is surfaced.
/// </remarks>
public static class AtomicFileReplace
{
    private const int Attempts = 20;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Replaces <paramref name="targetPath"/> with <paramref name="sourcePath"/>.
    /// </summary>
    /// <param name="sourcePath">The fully written temporary file.</param>
    /// <param name="targetPath">The file to replace (created when missing).</param>
    /// <param name="cancellationToken">Cancels waiting between attempts.</param>
    public static async Task ReplaceAsync(string sourcePath, string targetPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(sourcePath, targetPath, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < Attempts)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }
}
