using System.Xml.Linq;

namespace Spectara.Revela.Tests.Core;

/// <summary>
/// Guards repository-relative paths in the root MSBuild files against casing drift.
/// </summary>
/// <remarks>
/// Windows and macOS resolve <c>src\Sdk\Build</c> to the tracked <c>src/Sdk/build</c> folder, but
/// Linux does not. An <c>Exists(...)</c>-guarded import then silently turns into a no-op and
/// release packages built on Linux lose their private dependencies and <c>.deps.json</c>. This
/// check compares every segment ordinally so it fails on every OS.
/// </remarks>
[TestClass]
[TestCategory("Unit")]
public sealed class BuildFilePathCasingTests
{
    private const string ThisFileDirectory = "$(MSBuildThisFileDirectory)";

    [TestMethod]
    [DataRow("Directory.Build.props")]
    [DataRow("Directory.Build.targets")]
    public void RootBuildFile_ImportsAndProjectReferences_MatchOnDiskCasing(string buildFile)
    {
        var repoRoot = RepoRoot();
        var document = XDocument.Load(Path.Combine(repoRoot, buildFile));

        var paths = document.Descendants()
            .Where(element => element.Name.LocalName is "Import" or "ProjectReference")
            .Select(element => (string?)element.Attribute("Project") ?? (string?)element.Attribute("Include"))
            .OfType<string>()
            .Where(value => value.StartsWith(ThisFileDirectory, StringComparison.Ordinal))
            .Select(value => value[ThisFileDirectory.Length..])
            .ToList();

        var mismatches = paths.Where(path => !ExistsWithExactCasing(repoRoot, path)).ToList();

        Assert.IsEmpty(
            mismatches,
            $"{buildFile} references paths that do not exist with this exact casing: {string.Join(", ", mismatches)}");
    }

    private static bool ExistsWithExactCasing(string root, string relativePath)
    {
        var current = root;
        foreach (var segment in relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(current))
            {
                return false;
            }

            var match = Directory.EnumerateFileSystemEntries(current)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => string.Equals(name, segment, StringComparison.Ordinal));
            if (match is null)
            {
                return false;
            }

            current = Path.Combine(current, match);
        }

        return true;
    }

    private static string RepoRoot()
    {
        // Walk up from the test output instead of using [CallerFilePath]: deterministic CI builds
        // map source paths to "/_/..." which does not exist at runtime.
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Spectara.Revela.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (searched upward from '{AppContext.BaseDirectory}' for Spectara.Revela.slnx).");
    }
}
