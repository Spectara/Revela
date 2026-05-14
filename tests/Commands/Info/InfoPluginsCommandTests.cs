using NSubstitute;

using Spectara.Revela.Commands.Info;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Commands.Info;

[TestClass]
[TestCategory("Unit")]
public sealed class InfoPluginsCommandTests
{
    [TestMethod]
    public void Create_ReturnsPluginsCommandWithDescription()
    {
        var command = CreateCommand([]).Create();

        Assert.AreEqual("plugins", command.Name);
        Assert.IsFalse(string.IsNullOrEmpty(command.Description));
    }

    [TestMethod]
    public void Create_HasNoOptionsOrArguments()
    {
        // plugins listing takes no arguments — read-only diagnostic.
        var command = CreateCommand([]).Create();

        Assert.IsEmpty(command.Arguments);
    }

    [TestMethod]
    public void Create_HasNoSubcommandsByDefault()
    {
        // Per-plugin detail subcommands are added by plugins themselves via
        // ParentCommand: "info plugins" — not by this command.
        var command = CreateCommand([]).Create();

        Assert.IsEmpty(command.Subcommands);
    }

    private static InfoPluginsCommand CreateCommand(IReadOnlyList<LoadedPluginInfo> plugins)
    {
        var packageContext = Substitute.For<IPackageContext>();
        packageContext.Plugins.Returns(plugins);
        return new InfoPluginsCommand(packageContext);
    }
}
