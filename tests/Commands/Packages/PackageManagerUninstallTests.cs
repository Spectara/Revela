using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class PackageManagerUninstallTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Uninstall_PackageIdEscapesPluginDirectory_ThrowsAndDeletesNothing(bool absolute)
    {
        var victim = CreateDirectoryWithFile(
            Path.Combine(ConfigPathResolver.ConfigDirectory, $"revela-uninstall-victim-{Guid.NewGuid():N}"));
        try
        {
            var packageId = absolute
                ? victim
                : $"Spectara.Revela.Plugins.Guard/../../{Path.GetFileName(victim)}";
            using var httpClient = new HttpClient();
            var manager = CreateManager(httpClient);

            Assert.ThrowsExactly<ArgumentException>(() => manager.Uninstall(packageId));

            Assert.IsTrue(File.Exists(Path.Combine(victim, "keep.txt")));
        }
        finally
        {
            if (Directory.Exists(victim))
            {
                Directory.Delete(victim, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Uninstall_PluginDirectoryIsLink_ThrowsAndKeepsLinkTarget()
    {
        using var workspace = TestProject.Create();
        var victim = CreateDirectoryWithFile(Path.Combine(workspace.RootPath, "victim"));
        var packageId = $"Spectara.Revela.Plugins.GuardLink{Guid.NewGuid():N}";
        var link = Path.Combine(PackageManager.PluginDirectory, packageId);
        Directory.CreateDirectory(PackageManager.PluginDirectory);
        DirectoryLinkTestHelper.Create(link, victim);
        try
        {
            using var httpClient = new HttpClient();
            var manager = CreateManager(httpClient);

            Assert.ThrowsExactly<InvalidOperationException>(() => manager.Uninstall(packageId));

            Assert.IsTrue(File.Exists(Path.Combine(victim, "keep.txt")));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(link);
        }
    }

    [TestMethod]
    public void Uninstall_InstalledPlugin_DeletesPluginDirectory()
    {
        var packageId = $"Spectara.Revela.Plugins.GuardFixture{Guid.NewGuid():N}";
        var pluginPath = CreateDirectoryWithFile(Path.Combine(PackageManager.PluginDirectory, packageId));
        try
        {
            using var httpClient = new HttpClient();
            var manager = CreateManager(httpClient);

            var removed = manager.Uninstall(packageId);

            Assert.IsTrue(removed);
            Assert.IsFalse(Directory.Exists(pluginPath));
        }
        finally
        {
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
            }
        }
    }

    private static PackageManager CreateManager(HttpClient httpClient) =>
        new(
            httpClient,
            new NupkgExtractor(NullLogger<NupkgExtractor>.Instance),
            NullLogger<PackageManager>.Instance,
            Substitute.For<INuGetSourceManager>(),
            Substitute.For<IBuildInfo>());

    private static string CreateDirectoryWithFile(string path)
    {
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "keep.txt"), "data");
        return path;
    }
}
