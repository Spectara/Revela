using System.Text.Json;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Tests.Core.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class PackageIndexServiceTests
{
    private static readonly string[] ExpectedPlugins = ["Spectara.Revela.Plugins.Statistics", "spectara.revela.plugins.lowercase"];

    private string root = null!;

    [TestInitialize]
    public void Initialize() => root = Directory.CreateTempSubdirectory("revela-index-").FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public async Task SearchByTypeAsync_IndexWithForeignPrefixes_ReturnsOnlyOfficialPackages()
    {
        var service = await CreateServiceAsync(
            "Spectara.Revela.Plugins.Statistics",
            "spectara.revela.plugins.lowercase",
            "Evil.Spectara.Revela.Plugins.Squat",
            "Evil.Plugins.Core.Backdoor",
            "Spectara.RevelaEvil");

        var plugins = await service.SearchByTypeAsync("RevelaPlugin");

        CollectionAssert.AreEquivalent(ExpectedPlugins, plugins.Select(p => p.Id).ToArray());
    }

    [TestMethod]
    public async Task FindPackageAsync_ForeignPrefix_ReturnsNull()
    {
        var service = await CreateServiceAsync("Evil.Plugins.Core.Backdoor");

        var entry = await service.FindPackageAsync("Evil.Plugins.Core.Backdoor");

        Assert.IsNull(entry);
    }

    private async Task<PackageIndexService> CreateServiceAsync(params string[] ids)
    {
        var index = new PackageIndex
        {
            LastUpdated = DateTime.UtcNow,
            Packages = [.. ids.Select(id => new PackageIndexEntry { Id = id, Version = "1.0.0", Source = "test", Types = ["RevelaPlugin"] })]
        };
        var path = Path.Combine(root, "packages.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(index, PackageIndexJsonContext.Default.PackageIndex));
        return new PackageIndexService(TimeProvider.System, path);
    }
}
