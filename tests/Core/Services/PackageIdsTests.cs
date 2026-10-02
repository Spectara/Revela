using Spectara.Revela.Core;

namespace Spectara.Revela.Tests.Core.Services;

/// <summary>
/// Unit tests for <see cref="PackageSearchService"/> naming-convention inference.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class PackageSearchServiceTests
{
    [TestMethod]
    [DataRow("Spectara.Revela.Plugins.Statistics")]
    [DataRow("YourName.Revela.Plugin.Example")]
    [DataRow("acme.revela.plugins.cool")]
    public void InferPackageTypes_PluginNaming_ReturnsRevelaPlugin(string packageId)
    {
        var types = PackageSearchService.InferPackageTypes(packageId);

        Assert.Contains("RevelaPlugin", types);
    }

    [TestMethod]
    [DataRow("Spectara.Revela.Themes.Lumina")]
    [DataRow("YourName.Revela.Theme.Example")]
    [DataRow("acme.revela.themes.dark")]
    public void InferPackageTypes_ThemeNaming_ReturnsRevelaTheme(string packageId)
    {
        var types = PackageSearchService.InferPackageTypes(packageId);

        Assert.Contains("RevelaTheme", types);
    }

    [TestMethod]
    [DataRow("Newtonsoft.Json")]
    [DataRow("Spectara.Revela.Sdk")]
    [DataRow("Some.Random.Package")]
    public void InferPackageTypes_UnrelatedNaming_ReturnsEmpty(string packageId)
    {
        var types = PackageSearchService.InferPackageTypes(packageId);

        Assert.IsEmpty(types);
    }

    [TestMethod]
    public void InferPackageTypes_PluginSegmentNotTheme_DoesNotReturnTheme()
    {
        var types = PackageSearchService.InferPackageTypes("Spectara.Revela.Plugins.Statistics");

        Assert.DoesNotContain("RevelaTheme", types);
    }
}
