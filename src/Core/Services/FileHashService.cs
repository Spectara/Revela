using System.Security.Cryptography;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// File hash service using partial content hashing for fast change detection.
/// </summary>
/// <remarks>
/// <para>
/// Algorithm: SHA256(FileSize + FirstChunk + LastChunk) → 12 hex characters
/// </para>
/// <para>
/// Performance characteristics:
/// </para>
/// <list type="bullet">
///   <item><description>Small files (≤128KB): ~1ms (full content read)</description></item>
///   <item><description>Large files (>128KB): ~3-5ms (partial read, 128KB total)</description></item>
///   <item><description>Full SHA256 comparison: ~50-100ms for 10MB image</description></item>
/// </list>
/// </remarks>
public sealed class FileHashService : IFileHashService
{
    /// <summary>
    /// Size of each chunk to read (64KB).
    /// </summary>
    /// <remarks>
    /// 64KB is chosen because:
    /// - JPEG/EXIF headers fit within first 64KB
    /// - Large enough to capture meaningful content changes
    /// - Small enough for fast I/O (~1ms per chunk on SSD)
    /// </remarks>
    private const int ChunkSize = 64 * 1024;

    /// <inheritdoc />
    public string ComputeHash(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("File not found for hashing", filePath);
        }

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: ChunkSize,
            FileOptions.SequentialScan);

        var fileSize = fileInfo.Length;

        // For small files, hash entire content
        if (fileSize <= ChunkSize * 2)
        {
            var content = new byte[fileSize];
            stream.ReadExactly(content);
            return ComputeSha256Hash(fileSize, content);
        }

        // Read first 64KB
        var firstChunk = new byte[ChunkSize];
        stream.ReadExactly(firstChunk);

        // Read last 64KB
        var lastChunk = new byte[ChunkSize];
        stream.Seek(-ChunkSize, SeekOrigin.End);
        stream.ReadExactly(lastChunk);

        return ComputeSha256Hash(fileSize, firstChunk, lastChunk);
    }

    /// <summary>
    /// Compute SHA256 hash from file size and content chunks.
    /// </summary>
    private static string ComputeSha256Hash(long fileSize, params byte[][] chunks)
    {
        // Use incremental hashing to avoid concatenating large arrays
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        // Include file size as 8-byte little-endian value
        Span<byte> sizeBytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(sizeBytes, fileSize);
        sha256.AppendData(sizeBytes);

        // Include each content chunk
        foreach (var chunk in chunks)
        {
            sha256.AppendData(chunk);
        }

        // Get hash and return first 12 hex characters
        Span<byte> hashBytes = stackalloc byte[32];
        sha256.GetHashAndReset(hashBytes);

        return Convert.ToHexString(hashBytes[..6]);
    }
}
