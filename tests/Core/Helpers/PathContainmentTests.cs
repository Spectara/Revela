using Spectara.Revela.Core.Helpers;

namespace Spectara.Revela.Tests.Core.Helpers;

[TestClass]
[TestCategory("Unit")]
public sealed class PathContainmentTests
{
    private static readonly bool PathsIgnoreCase = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "revela-containment", "plugins");

    [TestMethod]
    [DataRow("Spectara.Revela.Plugins.Serve")]
    [DataRow("a/b/c")]
    [DataRow("..data")]
    [DataRow("a/../b")]
    [DataRow("Spectara.Revela.Plugins.Serve/")]
    public void IsStrictlyInside_PathBelowContainer_ReturnsTrue(string relative)
    {
        var result = PathContainment.IsStrictlyInside(Root, Path.Combine(Root, relative));

        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("/")]
    [DataRow(".")]
    [DataRow("a/..")]
    [DataRow("..")]
    [DataRow("../plugins-evil")]
    [DataRow("../plugins-evil/x")]
    [DataRow("../../outside")]
    public void IsStrictlyInside_SameOrOutsideContainer_ReturnsFalse(string relative)
    {
        var result = PathContainment.IsStrictlyInside(Root, Path.Combine(Root, relative));

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsStrictlyInside_ContainerWithTrailingSeparator_ComparesNormalizedPaths()
    {
        var container = Root + Path.DirectorySeparatorChar;

        Assert.IsTrue(PathContainment.IsStrictlyInside(container, Path.Combine(Root, "child")));
        Assert.IsFalse(PathContainment.IsStrictlyInside(container, Root));
    }

    [TestMethod]
    public void IsStrictlyInside_SiblingWithSharedPrefix_ReturnsFalse()
    {
        var result = PathContainment.IsStrictlyInside(Root, Root + "-evil");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsStrictlyInside_DifferentCase_FollowsPlatformCaseSensitivity()
    {
        var differentCase = Path.Combine(Path.GetDirectoryName(Root)!, "PLUGINS", "child");

        var result = PathContainment.IsStrictlyInside(Root, differentCase);

        Assert.AreEqual(PathsIgnoreCase, result);
    }

    [TestMethod]
    [DataRow("", true)]
    [DataRow("child", true)]
    [DataRow("..", false)]
    [DataRow("../plugins-evil", false)]
    public void IsSameOrInside_ComparedToContainer_IncludesContainerItself(string relative, bool expected)
    {
        var result = PathContainment.IsSameOrInside(Root, Path.Combine(Root, relative));

        Assert.AreEqual(expected, result);
    }
}
