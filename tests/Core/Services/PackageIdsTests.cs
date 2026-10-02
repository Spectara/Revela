using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Tests.Core.Services;

/// <summary>
/// Unit tests for <see cref="PackageIds"/>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class PackageIdsTests
{
    [TestMethod]
    [DataRow("Spectara.Revela.Plugins.Statistics")]
    [DataRow("YourName.Revela.Plugin.Example")]
    [DataRow("acme.revela.plugins.cool")]
    public void InferPackageTypes_PluginNaming_ReturnsRevelaPlugin(string packageId)
    {
        var types = PackageIds.InferPackageTypes(packageId);

        CollectionAssert.AreEqual(new[] { PackageIds.PluginPackageType }, types.ToArray());
    }

    [TestMethod]
    [DataRow("Spectara.Revela.Themes.Lumina")]
    [DataRow("Spectara.Revela.Themes.Lumina.Statistics")]
    [DataRow("YourName.Revela.Theme.Example")]
    [DataRow("acme.revela.themes.dark")]
    public void InferPackageTypes_ThemeNaming_ReturnsRevelaTheme(string packageId)
    {
        var types = PackageIds.InferPackageTypes(packageId);

        CollectionAssert.AreEqual(new[] { PackageIds.ThemePackageType }, types.ToArray());
    }

    [TestMethod]
    [DataRow("Newtonsoft.Json")]
    [DataRow("Spectara.Revela.Sdk")]
    [DataRow("Some.Random.Package")]
    public void InferPackageTypes_UnrelatedNaming_ReturnsEmpty(string packageId)
    {
        var types = PackageIds.InferPackageTypes(packageId);

        Assert.IsEmpty(types);
    }

    [TestMethod]
    [DataRow("OneDrive", "Spectara.Revela.Plugins.OneDrive")]
    [DataRow("Source.OneDrive", "Spectara.Revela.Plugins.Source.OneDrive")]
    [DataRow("Spectara.Revela.Plugins.Statistics", "Spectara.Revela.Plugins.Statistics")]
    [DataRow("spectara.revela.themes.Lumina.Statistics", "spectara.revela.themes.Lumina.Statistics")]
    public void FromPluginName_ExpandsShortNamesOnly(string name, string expected) =>
        Assert.AreEqual(expected, PackageIds.FromPluginName(name));

    [TestMethod]
    [DataRow("Lumina", "Spectara.Revela.Themes.Lumina")]
    [DataRow("Lumina.Statistics", "Spectara.Revela.Themes.Lumina.Statistics")]
    [DataRow("Spectara.Revela.Themes.Lumina", "Spectara.Revela.Themes.Lumina")]
    [DataRow("Spectara.Revela.Plugins.Serve", "Spectara.Revela.Plugins.Serve")]
    public void FromThemeName_ExpandsShortNamesOnly(string name, string expected) =>
        Assert.AreEqual(expected, PackageIds.FromThemeName(name));

    [TestMethod]
    [DataRow("Spectara.Revela.Themes.Lumina.Calendar", "Lumina.Calendar")]
    [DataRow("Spectara.Revela.Plugins.Source.OneDrive", "Source.OneDrive")]
    [DataRow("Acme.Revela.Watermark", "Acme.Revela.Watermark")]
    [DataRow("Spectara.Revela.Themes.", "Spectara.Revela.Themes.")]
    public void ToShortName_StripsOfficialPrefixOnly(string packageId, string expected) =>
        Assert.AreEqual(expected, PackageIds.ToShortName(packageId));
}
