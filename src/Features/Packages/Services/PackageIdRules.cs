using NuGet.Packaging;

namespace Spectara.Revela.Features.Packages.Services;

/// <summary>
/// NuGet package ID validation for IDs that are used to build file system paths.
/// </summary>
internal static class PackageIdRules
{
    /// <summary>
    /// Returns whether <paramref name="packageId"/> is a valid NuGet package ID.
    /// </summary>
    /// <remarks>
    /// Valid IDs contain only word characters separated by single <c>.</c> or <c>-</c>,
    /// so they cannot contain directory separators, <c>..</c> segments, or drive roots.
    /// </remarks>
    public static bool IsValid(string? packageId) =>
        !string.IsNullOrEmpty(packageId) &&
        packageId.Length <= PackageIdValidator.MaxPackageIdLength &&
        PackageIdValidator.IsValidPackageId(packageId);

    /// <summary>
    /// Returns whether <paramref name="path"/> resolves to a location strictly inside <paramref name="rootDirectory"/>.
    /// </summary>
    public static bool IsContainedIn(string rootDirectory, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(rootDirectory), Path.GetFullPath(path));
        return relative != "." &&
               !Path.IsPathRooted(relative) &&
               relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
