using System.Globalization;
using System.IO.Compression;

namespace Spectara.Revela.Plugin.Compress.Services;

/// <summary>
/// Compression statistics for a single format (Gzip or Brotli).
/// </summary>
public sealed record CompressionFormatStats
{
    /// <summary>Format name (e.g., "Gzip", "Brotli").</summary>
    public required string Format { get; init; }

    /// <summary>Number of files compressed.</summary>
    public int FileCount { get; set; }

    /// <summary>Total original size in bytes.</summary>
    public long OriginalSize { get; set; }

    /// <summary>Total compressed size in bytes.</summary>
    public long CompressedSize { get; set; }

    /// <summary>Compression ratio as percentage (0-100).</summary>
    public double SavingsPercent => OriginalSize > 0
        ? (1.0 - ((double)CompressedSize / OriginalSize)) * 100
        : 0;
}

/// <summary>
/// Overall compression statistics.
/// </summary>
public sealed record CompressionStats
{
    /// <summary>Gzip compression statistics.</summary>
    public CompressionFormatStats Gzip { get; } = new() { Format = "Gzip" };

    /// <summary>Brotli compression statistics.</summary>
    public CompressionFormatStats Brotli { get; } = new() { Format = "Brotli" };

    /// <summary>Number of files skipped (too small).</summary>
    public int SkippedCount { get; set; }

    /// <summary>Total files processed.</summary>
    public int TotalFiles => Gzip.FileCount;
}

/// <summary>
/// Service for compressing files with Gzip and Brotli.
/// </summary>
/// <remarks>
/// Uses .NET built-in compression streams - no external dependencies needed.
/// <list type="bullet">
/// <item><see cref="GZipStream"/> - RFC 1952 compliant</item>
/// <item><see cref="BrotliStream"/> - RFC 7932 compliant</item>
/// </list>
/// </remarks>
public sealed partial class CompressionService(ILogger<CompressionService> logger)
{
    /// <summary>Minimum file size to compress (256 bytes).</summary>
    private const int MinFileSizeBytes = 256;

    /// <summary>Gzip compression level (maximum).</summary>
    private const CompressionLevel GzipLevel = CompressionLevel.SmallestSize;

    /// <summary>File extensions to compress.</summary>
    private static readonly HashSet<string> CompressibleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html",
        ".css",
        ".js",
        ".json",
        ".svg",
        ".xml"
    };

    /// <summary>
    /// Compresses all eligible files in a directory.
    /// </summary>
    /// <param name="outputPath">Directory to scan for files.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compression statistics.</returns>
    public async Task<CompressionStats> CompressDirectoryAsync(
        string outputPath,
        IProgress<(int current, int total, string fileName)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stats = new CompressionStats();

        // Find all compressible files
        var files = Directory.EnumerateFiles(outputPath, "*.*", SearchOption.AllDirectories)
            .Where(f => CompressibleExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (files.Count == 0)
        {
            LogNoFilesFound(logger, outputPath);
            return stats;
        }

        LogFoundFiles(logger, files.Count, outputPath);

        var processedCount = 0;
        var lockObj = new object();

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            },
            async (filePath, ct) =>
            {
                var fileInfo = new FileInfo(filePath);

                // Skip small files
                if (fileInfo.Length < MinFileSizeBytes)
                {
                    lock (lockObj)
                    {
                        stats.SkippedCount++;
                    }
                    LogSkippedSmallFile(logger, filePath, fileInfo.Length);
                    return;
                }

                // Read original content once
                var content = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
                var originalSize = content.Length;

                // Compress with both formats
                var gzipSize = await CompressGzipAsync(filePath, content, ct).ConfigureAwait(false);
                var brotliSize = await CompressBrotliAsync(filePath, content, ct).ConfigureAwait(false);

                // Update statistics (thread-safe)
                lock (lockObj)
                {
                    stats.Gzip.FileCount++;
                    stats.Gzip.OriginalSize += originalSize;
                    stats.Gzip.CompressedSize += gzipSize;

                    stats.Brotli.FileCount++;
                    stats.Brotli.OriginalSize += originalSize;
                    stats.Brotli.CompressedSize += brotliSize;

                    processedCount++;
                }

                progress?.Report((processedCount, files.Count, Path.GetFileName(filePath)));
            }).ConfigureAwait(false);

        LogCompressionComplete(logger, stats.TotalFiles, stats.SkippedCount);

        return stats;
    }

    /// <summary>
    /// Compresses content with Gzip and writes to .gz file.
    /// </summary>
    private async Task<long> CompressGzipAsync(string filePath, byte[] content, CancellationToken ct)
    {
        var gzipPath = filePath + ".gz";

        await using var outputStream = File.Create(gzipPath);
        await using var gzipStream = new GZipStream(outputStream, GzipLevel);
        await gzipStream.WriteAsync(content, ct).ConfigureAwait(false);
        await gzipStream.FlushAsync(ct).ConfigureAwait(false);

        // Need to close streams to get accurate file size
        await gzipStream.DisposeAsync().ConfigureAwait(false);
        await outputStream.DisposeAsync().ConfigureAwait(false);

        var size = new FileInfo(gzipPath).Length;
        LogCompressedFile(logger, "Gzip", filePath, content.Length, size);
        return size;
    }

    /// <summary>
    /// Compresses content with Brotli and writes to .br file.
    /// </summary>
    private async Task<long> CompressBrotliAsync(string filePath, byte[] content, CancellationToken ct)
    {
        var brotliPath = filePath + ".br";

        await using var outputStream = File.Create(brotliPath);
        await using var brotliStream = new BrotliStream(outputStream, CompressionLevel.SmallestSize);

        // Set Brotli quality to maximum (11)
        // Note: BrotliStream uses CompressionLevel enum, SmallestSize = quality 11
        await brotliStream.WriteAsync(content, ct).ConfigureAwait(false);
        await brotliStream.FlushAsync(ct).ConfigureAwait(false);

        // Need to close streams to get accurate file size
        await brotliStream.DisposeAsync().ConfigureAwait(false);
        await outputStream.DisposeAsync().ConfigureAwait(false);

        var size = new FileInfo(brotliPath).Length;
        LogCompressedFile(logger, "Brotli", filePath, content.Length, size);
        return size;
    }

    /// <summary>
    /// Formats a byte size to human-readable string.
    /// </summary>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
        < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} KB", bytes / 1024.0),
        _ => string.Format(CultureInfo.InvariantCulture, "{0:0.##} MB", bytes / (1024.0 * 1024.0))
    };

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "No compressible files found in {OutputPath}")]
    private static partial void LogNoFilesFound(ILogger logger, string outputPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} compressible files in {OutputPath}")]
    private static partial void LogFoundFiles(ILogger logger, int count, string outputPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipped small file: {FilePath} ({Size} bytes)")]
    private static partial void LogSkippedSmallFile(ILogger logger, string filePath, long size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Format}: {FilePath} ({OriginalSize} → {CompressedSize})")]
    private static partial void LogCompressedFile(ILogger logger, string format, string filePath, long originalSize, long compressedSize);

    [LoggerMessage(Level = LogLevel.Information, Message = "Compression complete: {Count} files, {Skipped} skipped")]
    private static partial void LogCompressionComplete(ILogger logger, int count, int skipped);

    #endregion
}
