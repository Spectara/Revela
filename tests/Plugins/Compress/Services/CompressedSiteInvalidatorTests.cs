using NSubstitute;

using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class CompressedSiteInvalidatorTests
{
    private string testDirectory = null!;
    private CompressedSiteInvalidator invalidator = null!;

    [TestInitialize]
    public void Setup()
    {
        testDirectory = Path.Combine(
            Path.GetTempPath(),
            "revela-compress-invalidation-tests",
            Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDirectory);

        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(testDirectory);
        invalidator = new CompressedSiteInvalidator(pathResolver);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task InvalidateAsync_CompressedSidecarsExist_DeletesSidecarsOnly()
    {
        var nestedDirectory = Path.Combine(testDirectory, "assets");
        Directory.CreateDirectory(nestedDirectory);
        var original = Path.Combine(nestedDirectory, "main.css");
        var gzip = original + ".gz";
        var brotli = original + ".br";
        await File.WriteAllTextAsync(original, "current");
        await File.WriteAllTextAsync(gzip, "gzip");
        await File.WriteAllTextAsync(brotli, "brotli");

        var result = await invalidator.InvalidateAsync();

        Assert.IsTrue(result.Success);
        Assert.IsTrue(File.Exists(original));
        Assert.IsFalse(File.Exists(gzip));
        Assert.IsFalse(File.Exists(brotli));
    }

    [TestMethod]
    public async Task InvalidateAsync_DirectoryLinkLeavesArtifactRoot_PreservesExternalSidecars()
    {
        var externalDirectory = testDirectory + "-external";
        var linkPath = Path.Combine(testDirectory, "linked");
        Directory.CreateDirectory(externalDirectory);
        var externalSidecar = Path.Combine(externalDirectory, "external.css.gz");
        await File.WriteAllTextAsync(externalSidecar, "external");
        DirectoryLinkTestHelper.Create(linkPath, externalDirectory);

        try
        {
            var result = await invalidator.InvalidateAsync();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(File.Exists(externalSidecar));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(linkPath);
            Directory.Delete(externalDirectory, recursive: true);
        }
    }
}
