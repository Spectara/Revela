using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Commands.Info;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;
using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Info;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
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

    [TestMethod]
    public void Execute_StandaloneEdition_PointsToFullEditionForPackages()
    {
        var output = RunQuiet(CreateCommand(HostKind.Standalone).Create());

        Assert.Contains("Standalone edition", output);
        Assert.Contains("Full edition", output);
        Assert.DoesNotContain("embedded", output, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Execute_FullEdition_DoesNotShowPackageManagementNotice()
    {
        var output = RunQuiet(CreateCommand(HostKind.Full).Create());

        Assert.DoesNotContain("Package management", output);
    }

    [TestMethod]
    public void Execute_FullEdition_PointsToListCommands()
    {
        var output = RunQuiet(CreateCommand(HostKind.Full).Create());

        Assert.Contains("revela plugin list", output);
        Assert.Contains("revela theme list", output);
        Assert.DoesNotContain("info plugins", output);
        Assert.DoesNotContain("info themes", output);
    }

    [TestMethod]
    public void Execute_StandaloneEdition_PointsOnlyToAvailableListCommand()
    {
        // Standalone has no `plugin` command tree, so only `theme list` is a valid hint.
        var output = RunQuiet(CreateCommand(HostKind.Standalone).Create());

        Assert.Contains("revela theme list", output);
        Assert.DoesNotContain("plugin list", output);
    }

    [TestMethod]
    public void Execute_StandaloneEdition_ListsIncludedPluginsAndThemesWithVersions()
    {
        // Standalone has no `plugin list`, so `info` is the only place that shows what is built in.
        var output = RunQuiet(CreateCommand(HostKind.Standalone, withPackages: true).Create());

        Assert.Contains("Serve", output);
        Assert.Contains("2.1.0", output);
        Assert.Contains("Lumina", output);
        Assert.Contains("3.0.0-beta.1", output);
    }

    [TestMethod]
    public void Execute_FullEdition_ShowsCountsWithoutListingPackages()
    {
        var output = RunQuiet(CreateCommand(HostKind.Full, withPackages: true).Create());

        Assert.DoesNotContain("Serve", output);
        Assert.DoesNotContain("2.1.0", output);
        Assert.Contains("revela plugin list", output);
    }

    private static string RunQuiet(Command command)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 200;
        AnsiConsole.Console = console;

        try
        {
            Assert.AreEqual(0, command.Parse([]).Invoke());
            return writer.ToString();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private static InfoCommand CreateCommand(HostKind kind = HostKind.Full, bool withPackages = false)
    {
        var buildInfo = Substitute.For<IBuildInfo>();
        buildInfo.Kind.Returns(kind);
        buildInfo.FormatVersionLine().Returns(kind == HostKind.Standalone
            ? "revela 1.0.0 (.NET 10.0.4) \u2014 Standalone edition"
            : "revela 1.0.0 (.NET 10.0.4) \u2014 Full edition");
        buildInfo.InformationalVersion.Returns("1.0.0");
        buildInfo.Configuration.Returns("Debug");
        buildInfo.RuntimeIdentifier.Returns("linux-x64");

        var packageContext = Substitute.For<IPackageContext>();
        if (withPackages)
        {
            var plugin = Substitute.For<IPlugin>();
            plugin.Metadata.Returns(Metadata("Spectara.Revela.Plugins.Serve", "Serve", "2.1.0"));
            var theme = Substitute.For<ITheme>();
            theme.Metadata.Returns(Metadata("Spectara.Revela.Themes.Lumina", "Lumina", "3.0.0-beta.1"));
            packageContext.Plugins.Returns([new LoadedPluginInfo(plugin, PackageSource.Bundled)]);
            packageContext.Themes.Returns([new LoadedThemeInfo(theme, PackageSource.Bundled)]);
        }
        else
        {
            packageContext.Plugins.Returns([]);
            packageContext.Themes.Returns([]);
        }

        var themeConfig = Substitute.For<IOptionsMonitor<ThemeConfig>>();
        themeConfig.CurrentValue.Returns(new ThemeConfig());

        return new InfoCommand(buildInfo, packageContext, themeConfig);
    }

    private static PackageMetadata Metadata(string id, string name, string version) => new()
    {
        Id = id,
        Name = name,
        Version = version,
        Description = "Test package"
    };
}
