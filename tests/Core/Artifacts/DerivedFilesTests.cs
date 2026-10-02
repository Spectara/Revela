using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Core.Artifacts;

[TestClass]
[TestCategory("Unit")]
public sealed class DerivedFilesTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "revela-derived-files-tests",
        Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void DeleteAll_MatchingFilesInNestedDirectories_DeletesOnlyMatchingFiles()
    {
        var first = WriteFile(Path.Combine("a", "calendar.json"), "1234");
        var second = WriteFile(Path.Combine("b", "c [d]", "calendar.json"), "12");
        var unrelated = WriteFile(Path.Combine("a", "statistics.json"), "{}");

        var result = DerivedFiles.DeleteAll(root, "calendar.json");

        Assert.AreEqual(2, result.DeletedCount);
        Assert.AreEqual(6L, result.DeletedBytes);
        Assert.IsEmpty(result.Failures);
        Assert.IsFalse(File.Exists(first));
        Assert.IsFalse(File.Exists(second));
        Assert.IsTrue(File.Exists(unrelated));
    }

    [TestMethod]
    public void DeleteAll_MissingDirectory_DeletesNothing()
    {
        var result = DerivedFiles.DeleteAll(Path.Combine(root, "missing"), "calendar.json");

        Assert.AreEqual(0, result.DeletedCount);
        Assert.IsEmpty(result.Failures);
    }

    [TestMethod]
    public void DeleteAll_DirectoryLinkLeavesRoot_PreservesExternalFiles()
    {
        Directory.CreateDirectory(root);
        var external = root + "-external";
        Directory.CreateDirectory(external);
        var externalFile = Path.Combine(external, "calendar.json");
        File.WriteAllText(externalFile, "{}");
        var link = Path.Combine(root, "linked");
        DirectoryLinkTestHelper.Create(link, external);

        try
        {
            var result = DerivedFiles.DeleteAll(root, "calendar.json");

            Assert.AreEqual(0, result.DeletedCount);
            Assert.IsTrue(File.Exists(externalFile));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(link);
            Directory.Delete(external, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void DeleteAll_LockedFile_ReportsFailureAndContinues()
    {
        var locked = WriteFile(Path.Combine("a", "calendar.json"), "{}");
        var other = WriteFile(Path.Combine("b", "calendar.json"), "{}");

        DerivedFileDeletion result;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = DerivedFiles.DeleteAll(root, "calendar.json");
        }

        Assert.AreEqual(1, result.DeletedCount);
        Assert.HasCount(1, result.Failures);
        Assert.AreEqual(locked, result.Failures[0].Path);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Failures[0].Message));
        Assert.IsFalse(File.Exists(other));
    }

    private string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
