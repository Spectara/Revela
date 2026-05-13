using Spectara.Revela.Plugins.Source.OneDrive.Commands;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Commands;

/// <summary>
/// Tests for <see cref="OneDriveSourceCommand.TryResolveSafeDestination"/>.
/// Pins the path-traversal guard that protects OneDrive downloads from being
/// written outside the project source directory by a malicious share.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class TryResolveSafeDestinationTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "revela-onedrive-tests");

    [TestMethod]
    public void Accepts_SimpleRelativePath()
    {
        var ok = OneDriveSourceCommand.TryResolveSafeDestination(root, "photos/sunset.jpg", out var resolved);

        Assert.IsTrue(ok);
        Assert.IsTrue(resolved.StartsWith(Path.GetFullPath(root), StringComparison.Ordinal));
        Assert.IsTrue(resolved.EndsWith("sunset.jpg", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Accepts_NestedRelativePath()
    {
        var ok = OneDriveSourceCommand.TryResolveSafeDestination(root, "a/b/c.jpg", out var resolved);

        Assert.IsTrue(ok);
        Assert.IsTrue(resolved.StartsWith(Path.GetFullPath(root), StringComparison.Ordinal));
    }

    [TestMethod]
    public void Rejects_ParentTraversal()
    {
        var ok = OneDriveSourceCommand.TryResolveSafeDestination(root, "../etc/passwd", out _);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Rejects_NestedEscape()
    {
        var ok = OneDriveSourceCommand.TryResolveSafeDestination(root, "a/../../escape.txt", out _);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Rejects_AbsoluteUnixPath()
    {
        var ok = OneDriveSourceCommand.TryResolveSafeDestination(root, "/etc/passwd", out _);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Rejects_BackslashTraversal_OnAnyPlatform()
    {
        // A remote item could use '\' as separator; our normalizer must convert it
        // before Path.GetRelativePath, otherwise Linux would treat it as a literal
        // filename and miss the escape.
        var ok = OneDriveSourceCommand.TryResolveSafeDestination(root, "sub\\..\\..\\evil.txt", out _);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Rejects_EmptyAndNull()
    {
        Assert.IsFalse(OneDriveSourceCommand.TryResolveSafeDestination(root, "", out _));
        Assert.IsFalse(OneDriveSourceCommand.TryResolveSafeDestination(root, null!, out _));
    }
}
