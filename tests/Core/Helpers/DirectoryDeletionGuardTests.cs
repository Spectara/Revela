using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Core.Helpers;

[TestClass]
[TestCategory("Unit")]
public sealed class DirectoryDeletionGuardTests
{
    private static readonly bool PathsIgnoreCase = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    [TestMethod]
    [DataRow("output")]
    [DataRow("output/")]
    [DataRow("dist/site")]
    [DataRow("../www")]
    public void TryValidateOutputDirectory_OutputSeparateFromProjectData_ReturnsTrue(string output)
    {
        using var workspace = TestProject.Create();
        var (project, source) = CreateLayout(workspace.RootPath);

        var safe = DirectoryDeletionGuard.TryValidateOutputDirectory(
            Path.Combine(project, output), project, source, out var error);

        Assert.IsTrue(safe, error);
        Assert.IsNull(error);
    }

    [TestMethod]
    [DataRow(".", "the project directory")]
    [DataRow("./", "the project directory")]
    [DataRow("..", "the project directory")]
    [DataRow("source", "the source directory")]
    [DataRow("source/./", "the source directory")]
    [DataRow("photos/../source", "the source directory")]
    public void TryValidateOutputDirectory_OutputIsOrContainsProjectData_ReturnsFalse(string output, string expectedReason)
    {
        using var workspace = TestProject.Create();
        var (project, source) = CreateLayout(workspace.RootPath);

        var safe = DirectoryDeletionGuard.TryValidateOutputDirectory(
            Path.Combine(project, output), project, source, out var error);

        Assert.IsFalse(safe);
        Assert.Contains(expectedReason, error ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TryValidateOutputDirectory_OutputContainsExternalSource_ReturnsFalse()
    {
        using var workspace = TestProject.Create();
        var project = Path.Combine(workspace.RootPath, "site");
        var source = Path.Combine(workspace.RootPath, "library", "photos");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(source);

        var safe = DirectoryDeletionGuard.TryValidateOutputDirectory(
            Path.Combine(workspace.RootPath, "library"), project, source, out var error);

        Assert.IsFalse(safe);
        Assert.Contains("the source directory", error ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TryValidateOutputDirectory_FilesystemRoot_ReturnsFalse()
    {
        using var workspace = TestProject.Create();
        var (project, source) = CreateLayout(workspace.RootPath);
        var root = Path.GetPathRoot(project)!;

        var safe = DirectoryDeletionGuard.TryValidateOutputDirectory(root, project, source, out var error);

        Assert.IsFalse(safe);
        Assert.Contains("filesystem root", error ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TryValidateOutputDirectory_HomeDirectoryOrItsParent_ReturnsFalse()
    {
        using var workspace = TestProject.Create();
        var (project, source) = CreateLayout(workspace.RootPath);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            Assert.Inconclusive("No home directory is available on this machine.");
        }

        var homeSafe = DirectoryDeletionGuard.TryValidateOutputDirectory(home, project, source, out var error);
        var parentSafe = Path.GetDirectoryName(home) is { } parent &&
            DirectoryDeletionGuard.TryValidateOutputDirectory(parent, project, source, out _);

        Assert.IsFalse(homeSafe);
        Assert.IsFalse(parentSafe);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryValidateOutputDirectory_DifferentCaseOfProjectRoot_FollowsPlatformCaseSensitivity()
    {
        using var workspace = TestProject.Create();
        var (project, source) = CreateLayout(workspace.RootPath);
        var output = Path.Combine(Path.GetDirectoryName(project)!, Path.GetFileName(project).ToUpperInvariant());

        var safe = DirectoryDeletionGuard.TryValidateOutputDirectory(output, project, source, out _);

        Assert.AreEqual(!PathsIgnoreCase, safe);
    }

    [TestMethod]
    public void TryValidateOutputDirectory_OutputIsLinkToProject_ReturnsFalse()
    {
        using var workspace = TestProject.Create();
        var (project, source) = CreateLayout(workspace.RootPath);
        var link = Path.Combine(workspace.RootPath, "published");
        DirectoryLinkTestHelper.Create(link, project);
        try
        {
            var safe = DirectoryDeletionGuard.TryValidateOutputDirectory(link, project, source, out var error);

            Assert.IsFalse(safe);
            Assert.Contains("the project directory", error ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(link);
        }
    }

    [TestMethod]
    public void TryValidateOutputDirectory_SourceIsLinkIntoOutput_ReturnsFalse()
    {
        using var workspace = TestProject.Create();
        var project = Path.Combine(workspace.RootPath, "site");
        var library = Path.Combine(workspace.RootPath, "library");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(Path.Combine(library, "photos"));
        var source = Path.Combine(project, "source");
        DirectoryLinkTestHelper.Create(source, Path.Combine(library, "photos"));
        try
        {
            var safe = DirectoryDeletionGuard.TryValidateOutputDirectory(library, project, source, out var error);

            Assert.IsFalse(safe);
            Assert.Contains("the source directory", error ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(source);
        }
    }

    [TestMethod]
    [DataRow("Custom")]
    [DataRow("Custom/")]
    [DataRow("Nested/Custom")]
    [DataRow("..foo")]
    public void TryValidateContainedDirectory_TargetInsideContainer_ReturnsTrue(string relativeTarget)
    {
        using var workspace = TestProject.Create();
        var container = Path.Combine(workspace.RootPath, "themes");
        Directory.CreateDirectory(Path.Combine(container, "Custom"));

        var safe = DirectoryDeletionGuard.TryValidateContainedDirectory(
            Path.Combine(container, relativeTarget), container + Path.DirectorySeparatorChar, out var error);

        Assert.IsTrue(safe, error);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("../source")]
    [DataRow("Custom/..")]
    [DataRow("Custom/../..")]
    [DataRow("../themes-evil/Custom")]
    public void TryValidateContainedDirectory_TargetNotStrictlyInside_ReturnsFalse(string relativeTarget)
    {
        using var workspace = TestProject.Create();
        var container = Path.Combine(workspace.RootPath, "themes");
        Directory.CreateDirectory(container);

        var safe = DirectoryDeletionGuard.TryValidateContainedDirectory(
            Path.Combine(container, relativeTarget), container, out var error);

        Assert.IsFalse(safe);
        Assert.Contains("not strictly inside", error ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TryValidateContainedDirectory_AbsoluteTargetOutsideContainer_ReturnsFalse()
    {
        using var workspace = TestProject.Create();
        var container = Path.Combine(workspace.RootPath, "themes");
        var elsewhere = Path.Combine(workspace.RootPath, "elsewhere");

        var safe = DirectoryDeletionGuard.TryValidateContainedDirectory(
            Path.Combine(container, elsewhere), container, out var error);

        Assert.IsFalse(safe);
        Assert.Contains("not strictly inside", error ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TryValidateContainedDirectory_LinkInsideContainer_ReturnsFalse(bool linkIsParent)
    {
        using var workspace = TestProject.Create();
        var container = Path.Combine(workspace.RootPath, "themes");
        var victim = Path.Combine(workspace.RootPath, "victim");
        Directory.CreateDirectory(container);
        Directory.CreateDirectory(Path.Combine(victim, "Custom"));
        var link = Path.Combine(container, "Linked");
        DirectoryLinkTestHelper.Create(link, victim);
        try
        {
            var target = linkIsParent ? Path.Combine(link, "Custom") : link;

            var safe = DirectoryDeletionGuard.TryValidateContainedDirectory(target, container, out var error);

            Assert.IsFalse(safe);
            Assert.Contains("symbolic link or junction", error ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(link);
        }
    }

    private static (string Project, string Source) CreateLayout(string workspaceRoot)
    {
        var project = Path.Combine(workspaceRoot, "site");
        var source = Path.Combine(project, "source");
        Directory.CreateDirectory(source);
        return (project, source);
    }
}
