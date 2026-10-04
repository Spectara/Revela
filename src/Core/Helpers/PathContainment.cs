namespace Spectara.Revela.Core.Helpers;

/// <summary>
/// Lexical path containment checks.
/// </summary>
/// <remarks>
/// Both paths are normalized with <see cref="Path.GetFullPath(string)"/> and trailing separators
/// removed, then compared with <see cref="Path.GetRelativePath(string, string)"/>, which follows the
/// platform's path case sensitivity (case-insensitive on Windows and macOS). Links are not resolved;
/// see <see cref="DirectoryDeletionGuard"/> for link-aware deletion checks.
/// </remarks>
public static class PathContainment
{
    /// <summary>
    /// Returns whether <paramref name="path"/> lies strictly inside <paramref name="containerPath"/>.
    /// </summary>
    /// <param name="containerPath">The containing directory.</param>
    /// <param name="path">The path to check.</param>
    /// <returns><see langword="false"/> when the paths are equal or <paramref name="path"/> is outside.</returns>
    public static bool IsStrictlyInside(string containerPath, string path)
    {
        var relative = GetRelativePath(containerPath, path);
        return relative != "." && !IsOutside(relative);
    }

    /// <summary>
    /// Returns whether <paramref name="path"/> is <paramref name="containerPath"/> itself or lies inside it.
    /// </summary>
    /// <param name="containerPath">The containing directory.</param>
    /// <param name="path">The path to check.</param>
    public static bool IsSameOrInside(string containerPath, string path) =>
        !IsOutside(GetRelativePath(containerPath, path));

    /// <summary>
    /// Returns the full path of <paramref name="path"/> without a trailing directory separator.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    public static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string GetRelativePath(string containerPath, string path) =>
        Path.GetRelativePath(Normalize(containerPath), Normalize(path));

    private static bool IsOutside(string relative) =>
        Path.IsPathRooted(relative) ||
        relative == ".." ||
        relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
