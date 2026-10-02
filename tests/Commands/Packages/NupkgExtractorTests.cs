using Microsoft.Extensions.Logging.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Services;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Integration")]
public sealed class NupkgExtractorTests
{
    [TestMethod]
    [DataRow("../escaped")]
    [DataRow("..")]
    [DataRow("nested/escaped")]
    public async Task ExtractAsync_UnsafePackageIdInNuspec_FailsWithoutWritingOutsideTarget(string nuspecId)
    {
        var root = Directory.CreateTempSubdirectory("revela-extract-").FullName;
        try
        {
            var targetDir = Path.Combine(root, "plugins");
            _ = Directory.CreateDirectory(targetDir);
            var nupkg = TestPackageFactory.CreateRawPackage(Path.Combine(root, "crafted.nupkg"), nuspecId);
            var extractor = new NupkgExtractor(NullLogger<NupkgExtractor>.Instance);

            // No required type: the crafted nuspec declares none, so the ID check is what rejects it.
            var result = await extractor.ExtractAsync(nupkg, targetDir, requiredPackageType: null, CancellationToken.None);

            Assert.AreEqual(PackageInstallStatus.Failed, result.Status);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "escaped")));
            Assert.IsEmpty(Directory.GetFileSystemEntries(targetDir));
            Assert.IsEmpty(Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExtractAsync_ValidPackage_ExtractsIntoPackageSubdirectory()
    {
        var root = Directory.CreateTempSubdirectory("revela-extract-").FullName;
        try
        {
            var targetDir = Path.Combine(root, "plugins");
            var nupkg = TestPackageFactory.CreatePackage(Path.Combine(root, "feed"), "Spectara.Revela.Plugins.Fixture", "1.0.0");
            var extractor = new NupkgExtractor(NullLogger<NupkgExtractor>.Instance);

            var result = await extractor.ExtractAsync(nupkg, targetDir, PackageIds.PluginPackageType, CancellationToken.None);

            Assert.AreEqual(PackageInstallStatus.Installed, result.Status);
            Assert.IsTrue(File.Exists(Path.Combine(targetDir, "Spectara.Revela.Plugins.Fixture", "Spectara.Revela.Plugins.Fixture.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
