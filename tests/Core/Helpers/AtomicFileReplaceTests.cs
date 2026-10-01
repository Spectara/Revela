using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Core.Helpers;

[TestClass]
[TestCategory("Unit")]
public sealed class AtomicFileReplaceTests
{
    [TestMethod]
    public async Task ReplaceAsync_TargetBrieflyOpenWithoutDeleteSharing_ReplacesAfterReaderCloses()
    {
        using var workspace = TestProject.Create();
        var (source, target) = CreateFiles(workspace.RootPath);

        await using var reader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var releaseReader = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            await reader.DisposeAsync();
        });

        await AtomicFileReplace.ReplaceAsync(source, target);
        await releaseReader;

        Assert.AreEqual("new", await File.ReadAllTextAsync(target));
        Assert.IsFalse(File.Exists(source));
    }

    [TestMethod]
    public async Task ReplaceAsync_TargetHeldOpenThroughAllAttempts_ThrowsAndKeepsBothFiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows file sharing is required for this failure injection.");
        }

        using var workspace = TestProject.Create();
        var (source, target) = CreateFiles(workspace.RootPath);

        await using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => AtomicFileReplace.ReplaceAsync(source, target));
        }

        Assert.AreEqual("old", await File.ReadAllTextAsync(target));
        Assert.AreEqual("new", await File.ReadAllTextAsync(source));
    }

    private static (string Source, string Target) CreateFiles(string root)
    {
        var source = Path.Combine(root, "config.json.tmp");
        var target = Path.Combine(root, "config.json");
        File.WriteAllText(source, "new");
        File.WriteAllText(target, "old");
        return (source, target);
    }
}
