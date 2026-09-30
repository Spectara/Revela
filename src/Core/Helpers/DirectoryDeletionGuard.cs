using System.Diagnostics.CodeAnalysis;

namespace Spectara.Revela.Core.Helpers;

/// <summary>
/// Validates directories before they are deleted recursively.
/// </summary>
/// <remarks>
/// <para>
/// Paths are compared after <see cref="Path.GetFullPath(string)"/> normalization with trailing
/// separators removed. Containment is evaluated with <see cref="Path.GetRelativePath(string, string)"/>,
/// which uses the platform's path case sensitivity.
/// </para>
/// <para>
/// Symbolic links and junctions are never followed out of a containment root, and configured
/// output paths are also checked with links resolved so that a link cannot hide a protected
/// directory.
/// </para>
/// </remarks>
public static class DirectoryDeletionGuard
{
    private const int MaxLinkDepth = 32;

    /// <summary>
    /// Validates that a configured output directory can be deleted without destroying project data.
    /// </summary>
    /// <remarks>
    /// Refuses a filesystem root and any directory that is, or contains, the project directory,
    /// the source directory or the user's home directory.
    /// </remarks>
    /// <param name="outputPath">Resolved output directory.</param>
    /// <param name="projectPath">Project root directory.</param>
    /// <param name="sourcePath">Resolved source directory.</param>
    /// <param name="error">Reason the deletion is unsafe; <see langword="null"/> when safe.</param>
    /// <returns><see langword="true"/> when the output directory may be deleted.</returns>
    public static bool TryValidateOutputDirectory(
        string outputPath,
        string projectPath,
        string sourcePath,
        [NotNullWhen(false)] out string? error)
    {
        var outputCandidates = WithResolvedLinks(outputPath);

        if (outputCandidates.Any(IsFilesystemRoot))
        {
            error = $"Refusing to delete '{outputPath}': it is a filesystem root.";
            return false;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        (string Label, string Path)[] protectedDirectories =
        [
            ("the project directory", projectPath),
            ("the source directory", sourcePath),
            ("the user's home directory", home)
        ];

        foreach (var (label, protectedPath) in protectedDirectories)
        {
            if (string.IsNullOrWhiteSpace(protectedPath))
            {
                continue;
            }

            var protectedCandidates = WithResolvedLinks(protectedPath);
            if (outputCandidates.Any(output => protectedCandidates.Any(target => IsSameOrAncestor(output, target))))
            {
                error = $"Refusing to delete '{outputPath}': it is or contains {label} '{protectedPath}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a directory lies strictly inside a container and is not reached through a link.
    /// </summary>
    /// <param name="targetPath">Directory that is about to be deleted.</param>
    /// <param name="containerPath">Directory that must contain <paramref name="targetPath"/>.</param>
    /// <param name="error">Reason the deletion is unsafe; <see langword="null"/> when safe.</param>
    /// <returns><see langword="true"/> when the directory may be deleted.</returns>
    public static bool TryValidateContainedDirectory(
        string targetPath,
        string containerPath,
        [NotNullWhen(false)] out string? error)
    {
        var container = Normalize(containerPath);
        var target = Normalize(targetPath);
        var relative = Path.GetRelativePath(container, target);

        if (relative == "." || IsOutside(relative))
        {
            error = $"Refusing to delete '{targetPath}': it is not strictly inside '{containerPath}'.";
            return false;
        }

        var current = container;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            var info = new DirectoryInfo(current);
            if (!info.Exists && info.LinkTarget is null)
            {
                break;
            }

            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = $"Refusing to delete '{targetPath}': '{current}' is a symbolic link or junction.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsOutside(string relative) =>
        Path.IsPathRooted(relative) ||
        relative == ".." ||
        relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static bool IsSameOrAncestor(string ancestor, string path)
    {
        var relative = Path.GetRelativePath(ancestor, path);
        return relative == "." || !IsOutside(relative);
    }

    private static bool IsFilesystemRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        return !string.IsNullOrEmpty(root) && Path.GetRelativePath(root, path) == ".";
    }

    private static string[] WithResolvedLinks(string path)
    {
        var normalized = Normalize(path);
        var resolved = ResolveLinks(normalized, depth: 0);
        return resolved == normalized ? [normalized] : [normalized, resolved];
    }

    private static string ResolveLinks(string path, int depth)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root) || depth > MaxLinkDepth)
        {
            return path;
        }

        var segments = path[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        try
        {
            for (var i = 0; i < segments.Length; i++)
            {
                current = Path.Combine(current, segments[i]);
                var info = new DirectoryInfo(current);
                if (info.LinkTarget is null)
                {
                    if (!info.Exists)
                    {
                        return path;
                    }

                    continue;
                }

                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                return target is null
                    ? path
                    : ResolveLinks(Normalize(Path.Combine([target.FullName, .. segments[(i + 1)..]])), depth + 1);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unresolvable links fall back to the lexical path, which is always checked as well.
        }

        return path;
    }
}
