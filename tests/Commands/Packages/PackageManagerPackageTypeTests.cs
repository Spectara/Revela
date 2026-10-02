using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Services;
using Spectara.Revela.Sdk.Hosting;
using NuGetPackageSource = NuGet.Configuration.PackageSource;

namespace Spectara.Revela.Tests.Commands.Packages;

/// <summary>
/// The installer checks the nuspec package type before it writes any file, so installing a
/// package with the wrong type never touches the plugin directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class PackageManagerPackageTypeTests
{
    private const string ThemeId = "Spectara.Revela.Themes.Fixture";

    private string root = null!;
    private string feed = null!;
    private string targetDir = null!;

    [TestInitialize]
    public void Initialize()
    {
        root = Directory.CreateTempSubdirectory("revela-pm-type-").FullName;
        feed = Path.Combine(root, "feed");
        targetDir = Path.Combine(root, "plugins");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExtractFromNuGetAsync_WrongPackageType_ReportsTypesAndWritesNothing()
    {
        _ = TestPackageFactory.CreatePackage(feed, ThemeId, "1.0.0", PackageIds.ThemePackageType);
        using var httpClient = new HttpClient();
        var manager = CreateManager(httpClient);

        var result = await manager.ExtractFromNuGetAsync(
            ThemeId, null, PackageIds.PluginPackageType, Repository(), targetDir, CancellationToken.None);

        Assert.AreEqual(PackageInstallStatus.WrongPackageType, result.Status);
        Assert.IsNotNull(result.Package);
        CollectionAssert.AreEqual(new[] { PackageIds.ThemePackageType }, result.Package.PackageTypes.ToArray());
        Assert.IsFalse(Directory.Exists(Path.Combine(targetDir, ThemeId)));
    }

    [TestMethod]
    public async Task InstallFromNupkgAsync_WrongTypeForInstalledPackage_KeepsInstalledFiles()
    {
        var nupkg = TestPackageFactory.CreatePackage(feed, ThemeId, "2.0.0", PackageIds.ThemePackageType);
        var installedDir = Directory.CreateDirectory(Path.Combine(targetDir, ThemeId)).FullName;
        var installedDll = Path.Combine(installedDir, $"{ThemeId}.dll");
        await File.WriteAllBytesAsync(installedDll, [9]);
        using var httpClient = new HttpClient();
        var manager = CreateManager(httpClient);

        var result = await manager.InstallFromNupkgAsync(
            nupkg, PackageIds.PluginPackageType, targetDir, CancellationToken.None);

        Assert.AreEqual(PackageInstallStatus.WrongPackageType, result.Status);
        CollectionAssert.AreEqual(new byte[] { 9 }, await File.ReadAllBytesAsync(installedDll));
    }

    [TestMethod]
    [DataRow(PackageIds.ThemePackageType)]
    [DataRow(null)]
    public async Task ExtractFromNuGetAsync_RequiredTypeDeclaredOrUnchecked_Installs(string? requiredPackageType)
    {
        _ = TestPackageFactory.CreatePackage(feed, ThemeId, "1.0.0", PackageIds.ThemePackageType);
        using var httpClient = new HttpClient();
        var manager = CreateManager(httpClient);

        var result = await manager.ExtractFromNuGetAsync(
            ThemeId, null, requiredPackageType, Repository(), targetDir, CancellationToken.None);

        Assert.AreEqual(PackageInstallStatus.Installed, result.Status);
        Assert.IsTrue(File.Exists(Path.Combine(targetDir, ThemeId, $"{ThemeId}.dll")));
    }

    private SourceRepository Repository() => NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3(new NuGetPackageSource(feed));

    private static PackageManager CreateManager(HttpClient httpClient)
    {
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.LoadSourcesAsync(Arg.Any<CancellationToken>()).Returns([]);
        var buildInfo = Substitute.For<IBuildInfo>();
        buildInfo.Version.Returns("1.0.0");

        return new PackageManager(
            httpClient,
            new NupkgExtractor(NullLogger<NupkgExtractor>.Instance),
            NullLogger<PackageManager>.Instance,
            sourceManager,
            buildInfo);
    }
}
