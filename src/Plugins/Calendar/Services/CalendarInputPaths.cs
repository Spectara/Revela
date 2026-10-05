namespace Spectara.Revela.Plugins.Calendar.Services;

/// <summary>
/// Validates the paths a calendar page's front matter points to.
/// </summary>
/// <remarks>
/// The checks are lexical (links are not resolved), like Revela core's path containment helper,
/// which plugins cannot reference.
/// </remarks>
internal static class CalendarInputPaths
{
    /// <summary>Extension of the generated calendar data file.</summary>
    internal const string DataFileExtension = ".json";

    /// <summary>
    /// Resolves <c>calendar.source</c> relative to the page folder.
    /// </summary>
    /// <param name="sourcePath">The project's source folder.</param>
    /// <param name="pageDirectory">The folder of the page's <c>_index.revela</c>.</param>
    /// <param name="source">The front-matter value (relative path or absolute path).</param>
    /// <returns>The full path, or <see langword="null"/> when it is not inside the source folder.</returns>
    public static string? ResolveSource(string sourcePath, string pageDirectory, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
        var fullPath = Path.GetFullPath(Path.Combine(pageDirectory, source));
        var relative = Path.GetRelativePath(root, fullPath);

        var outside = Path.IsPathRooted(relative)
            || relative == "."
            || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        return outside ? null : fullPath;
    }

    /// <summary>
    /// Returns whether <paramref name="fileName"/> is a bare <c>*.json</c> file name
    /// (no folder, no drive, no <c>..</c>), as required for <c>data.calendar</c>.
    /// </summary>
    /// <param name="fileName">The front-matter value.</param>
    public static bool IsDataFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal)
        && fileName.Length > DataFileExtension.Length
        && fileName.EndsWith(DataFileExtension, StringComparison.Ordinal)
        && fileName.IndexOfAny(['/', '\\', ':']) < 0
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
