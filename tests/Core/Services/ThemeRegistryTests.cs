using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Tests.Core.Services;

[TestClass]
public sealed class ThemeRegistryTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ResolveInstalled_MissingThemeName_ResolvesDefaultTheme(string? themeName)
    {
        var lumina = InstalledTheme(ThemeConfig.DefaultName);
        var registry = new ThemeRegistry([lumina, InstalledTheme("Noir")], NullLogger<ThemeRegistry>.Instance);

        var resolved = registry.ResolveInstalled(themeName);

        Assert.AreSame(lumina, resolved);
    }

    [TestMethod]
    [DataRow(null, ThemeConfig.DefaultName)]
    [DataRow("", ThemeConfig.DefaultName)]
    [DataRow(" \t ", ThemeConfig.DefaultName)]
    [DataRow("Noir", "Noir")]
    public void ResolveName_ReturnsConfiguredNameOrDefault(string? configured, string expected) =>
        Assert.AreEqual(expected, ThemeConfig.ResolveName(configured));

    private static ITheme InstalledTheme(string name)
    {
        var theme = Substitute.For<ITheme>();
        theme.Metadata.Returns(new PackageMetadata
        {
            Id = "Test.Themes." + name,
            Name = name,
            Version = "1.0.0",
            Description = "Theme fixture"
        });
        theme.Prefix.Returns((string?)null);
        theme.TargetTheme.Returns((string?)null);
        return theme;
    }
}
