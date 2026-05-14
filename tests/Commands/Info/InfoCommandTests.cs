using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Commands.Info;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Tests.Commands.Info;

[TestClass]
[TestCategory("Unit")]
public sealed class InfoCommandTests
{
    [TestMethod]
    public void Create_ReturnsInfoCommandWithDescription()
    {
        var command = CreateCommand().Create();

        Assert.AreEqual("info", command.Name);
        Assert.IsFalse(string.IsNullOrEmpty(command.Description));
    }

    [TestMethod]
    public void Create_HasNoSubcommandsByDefault()
    {
        // Subcommands are attached by HostExtensions via ParentCommand: "info"
        // routing, not by InfoCommand itself.
        var command = CreateCommand().Create();

        Assert.IsEmpty(command.Subcommands);
    }

    [TestMethod]
    public void Create_HasNoOptions()
    {
        // info default action takes no arguments.
        var command = CreateCommand().Create();

        Assert.IsEmpty(command.Arguments);
    }

    private static InfoCommand CreateCommand()
    {
        var buildInfo = Substitute.For<IBuildInfo>();
        buildInfo.Kind.Returns(HostKind.Standalone);
        buildInfo.FormatVersionLine().Returns("revela 1.0.0 (.NET 10.0.4)");
        buildInfo.InformationalVersion.Returns("1.0.0");
        buildInfo.Configuration.Returns("Debug");
        buildInfo.RuntimeIdentifier.Returns("linux-x64");

        var packageContext = Substitute.For<IPackageContext>();
        packageContext.Plugins.Returns([]);
        packageContext.Themes.Returns([]);

        var themeConfig = Substitute.For<IOptionsMonitor<ThemeConfig>>();
        themeConfig.CurrentValue.Returns(new ThemeConfig());

        return new InfoCommand(buildInfo, packageContext, themeConfig);
    }
}
