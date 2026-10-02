namespace Spectara.Revela.Core.Services;

/// <summary>
/// Service for copying static files from source/_static/ to output root.
/// </summary>
public interface IStaticFileService
{
    /// <summary>
    /// Copies all files from source/_static/ to output root.
    /// </summary>
    /// <param name="sourcePath">Path to the source directory (containing _static/).</param>
    /// <param name="outputPath">Path to the output directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of files copied.</returns>
    Task<int> CopyStaticFilesAsync(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken = default);
}
