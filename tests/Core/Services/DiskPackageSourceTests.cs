using Microsoft.Extensions.Logging.Abstractions;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Features.Packages.Services;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Core.Services;

/// <summary>
/// The application directory is only scanned in Development (F5 / launchSettings), where
/// plugins and themes built next to the host are picked up without installing them.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class DiskPackageSourceTests
{
    private string pluginDirectory = null!;

    [TestInitialize]
    public void Initialize() => pluginDirectory = Directory.CreateTempSubdirectory("revela-plugins-").FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(pluginDirectory, recursive: true);

    [TestMethod]
    public void LoadThemes_ProductionEnvironment_IgnoresAssembliesNextToTheExecutable()
    {
        var source = CreateSource("Production");

        Assert.IsEmpty(source.LoadThemes());
        Assert.IsEmpty(source.LoadPlugins());
    }

    [TestMethod]
    public void LoadThemes_DevelopmentEnvironment_FindsThemesNextToTheExecutable()
    {
        var source = CreateSource("Development");

        var themes = source.LoadThemes();

        Assert.IsTrue(
            themes.Any(t => t.Theme is LuminaTheme && t.Source == PackageSource.Bundled),
            "Lumina is referenced by this test project, so it is in the application directory.");
    }

    [TestMethod]
    public void PackageOptions_Defaults_DoNotSearchApplicationDirectory() =>
        Assert.IsFalse(new PackageOptions().SearchApplicationDirectory);

    private DiskPackageSource CreateSource(string environmentName) =>
        new(PackageOptions.ForEnvironment(environmentName) with { PluginDirectory = pluginDirectory }, NullLoggerFactory.Instance);
}
