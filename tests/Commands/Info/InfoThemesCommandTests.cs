using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Commands.Info;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Tests.Commands.Info;

[TestClass]
[TestCategory("Unit")]
public sealed class InfoThemesCommandTests
{
    [TestMethod]
    public void Create_ReturnsThemesCommandWithDescription()
    {
        var command = CreateCommand([], activeThemeName: "").Create();

        Assert.AreEqual("themes", command.Name);
        Assert.IsFalse(string.IsNullOrEmpty(command.Description));
    }

    [TestMethod]
    public void Create_HasNoOptionsOrArguments()
    {
        var command = CreateCommand([], activeThemeName: "").Create();

        Assert.IsEmpty(command.Arguments);
    }

    [TestMethod]
    public void Create_HasNoSubcommandsByDefault()
    {
        // Per-theme detail subcommands are added by themes themselves via
        // ParentCommand: "info themes" — not by this command.
        var command = CreateCommand([], activeThemeName: "").Create();

        Assert.IsEmpty(command.Subcommands);
    }

    private static InfoThemesCommand CreateCommand(
        IReadOnlyList<LoadedThemeInfo> themes,
        string activeThemeName)
    {
        var packageContext = Substitute.For<IPackageContext>();
        packageContext.Themes.Returns(themes);

        var themeConfig = Substitute.For<IOptionsMonitor<ThemeConfig>>();
        themeConfig.CurrentValue.Returns(new ThemeConfig { Name = activeThemeName });

        return new InfoThemesCommand(packageContext, themeConfig);
    }
}
