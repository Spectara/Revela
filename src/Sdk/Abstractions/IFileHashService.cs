namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Service for computing file hashes for change detection.
/// </summary>
/// <remarks>
/// <para>
/// Provides fast, reliable file change detection suitable for incremental builds.
/// Uses partial content hashing (first/last 64KB + file size) for performance
/// while maintaining high reliability for real-world file changes.
/// </para>
/// <para>
/// This approach is significantly faster than full-file hashing (~5ms vs ~100ms for large images)
/// while still detecting virtually all practical changes (edits, re-exports, metadata changes).
/// </para>
/// <para>
/// Note: This is NOT suitable for cryptographic purposes or security-critical comparisons.
/// </para>
/// </remarks>
public interface IFileHashService
{
    /// <summary>
    /// Compute a hash for file change detection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hash is computed from: FileSize + first 64KB + last 64KB → SHA256 → 12 hex chars.
    /// For files ≤128KB, the entire content is hashed.
    /// </para>
    /// <para>
    /// The hash will change when:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>File size changes (any edit that adds/removes data)</description></item>
    ///   <item><description>File header changes (EXIF edits, format changes)</description></item>
    ///   <item><description>File trailer changes (most image edits)</description></item>
    /// </list>
    /// </remarks>
    /// <param name="filePath">Absolute path to the file</param>
    /// <returns>12-character hexadecimal hash string</returns>
    /// <exception cref="FileNotFoundException">If the file does not exist</exception>
    string ComputeHash(string filePath);
}
