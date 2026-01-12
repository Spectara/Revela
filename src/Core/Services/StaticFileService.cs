using Spectara.Revela.Sdk;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Service for copying static files from source/_static/ to output root.
/// </summary>
/// <remarks>
/// Static files are copied 1:1 preserving directory structure.
/// Example: source/_static/favicon/favicon.ico → output/favicon/favicon.ico
/// </remarks>
public sealed partial class StaticFileService(ILogger<StaticFileService> logger) : IStaticFileService
{
    /// <inheritdoc />
    public async Task<int> CopyStaticFilesAsync(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var staticPath = Path.Combine(sourcePath, ProjectPaths.Static);

        if (!Directory.Exists(staticPath))
        {
            LogNoStaticFolder(logger, staticPath);
            return 0;
        }

        var files = Directory.GetFiles(staticPath, "*", SearchOption.AllDirectories);

        if (files.Length == 0)
        {
            LogNoStaticFiles(logger, staticPath);
            return 0;
        }

        LogCopyingStaticFiles(logger, files.Length, staticPath);

        var copiedCount = 0;

        foreach (var sourceFile in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get relative path from _static folder
            var relativePath = Path.GetRelativePath(staticPath, sourceFile);
            var targetFile = Path.Combine(outputPath, relativePath);

            // Ensure target directory exists
            var targetDir = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Copy file
            await CopyFileAsync(sourceFile, targetFile, cancellationToken);
            LogCopiedFile(logger, relativePath);
            copiedCount++;
        }

        LogCopiedStaticFiles(logger, copiedCount);
        return copiedCount;
    }

    private static async Task CopyFileAsync(
        string sourceFile,
        string targetFile,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 81920; // 80KB buffer

        await using var sourceStream = new FileStream(
            sourceFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var targetStream = new FileStream(
            targetFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        await sourceStream.CopyToAsync(targetStream, bufferSize, cancellationToken);
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "No _static folder found at {Path}")]
    private static partial void LogNoStaticFolder(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No files in _static folder at {Path}")]
    private static partial void LogNoStaticFiles(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Copying {Count} static files from {Path}")]
    private static partial void LogCopyingStaticFiles(ILogger logger, int count, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied static file: {RelativePath}")]
    private static partial void LogCopiedFile(ILogger logger, string relativePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Copied {Count} static files to output")]
    private static partial void LogCopiedStaticFiles(ILogger logger, int count);

    #endregion
}
