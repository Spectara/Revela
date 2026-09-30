using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Theme.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Services;
using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Packages;

/// <summary>
/// Install commands must persist the exact installed version in <c>dependencies.packages</c>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class InstallCommandRegistrationTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("latest")]
    public async Task PluginInstall_WithoutExactVersion_RegistersInstalledVersion(string? requested)
    {
        const string packageId = "Spectara.Revela.Plugins.Fixture";
        var installer = Substitute.For<IPackageInstaller>();
        installer.InstallAsync(packageId, requested, null, Arg.Any<CancellationToken>())
            .Returns(new InstalledPackage(packageId, "1.4.2", ["RevelaPlugin"]));
        var index = Substitute.For<IPackageIndexService>();
        index.FindPackageAsync(packageId, Arg.Any<CancellationToken>())
            .Returns(new PackageIndexEntry { Id = packageId, Version = "9.9.9", Description = "Index", Source = "index", Types = ["RevelaPlugin"] });
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new PluginInstallCommand(
            NullLogger<PluginInstallCommand>.Instance, installer, index, globalConfig, NoProjectFeeds()).Create();

        var exitCode = await InvokeAsync(command, requested is null ? ["Fixture"] : ["Fixture", "--version", requested]);

        Assert.AreEqual(0, exitCode);
        await globalConfig.Received(1).AddPackageAsync(packageId, "1.4.2", Arg.Any<CancellationToken>());
        await globalConfig.DidNotReceive().AddPackageAsync(Arg.Any<string>(), "latest", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ThemeInstall_WithoutVersion_RegistersInstalledVersionNotIndexVersion()
    {
        const string packageId = "Spectara.Revela.Themes.Noir";
        var themeService = Substitute.For<IThemeService>();
        themeService.InstallAsync(packageId, null, null, Arg.Any<CancellationToken>())
            .Returns(new InstalledPackage(packageId, "3.1.0-beta.2", ["RevelaTheme"]));
        var index = Substitute.For<IPackageIndexService>();
        index.FindPackageAsync(packageId, Arg.Any<CancellationToken>())
            .Returns(new PackageIndexEntry { Id = packageId, Version = "3.0.0", Description = "Index", Source = "index", Types = ["RevelaTheme"] });
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new ThemeInstallCommand(
            NullLogger<ThemeInstallCommand>.Instance, index, themeService, globalConfig, [NoProjectFeeds()]).Create();

        var exitCode = await InvokeAsync(command, ["Noir"]);

        Assert.AreEqual(0, exitCode);
        await globalConfig.Received(1).AddPackageAsync(packageId, "3.1.0-beta.2", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task PluginInstall_ProjectFeedWithoutConsent_InstallsNothing()
    {
        var installer = Substitute.For<IPackageInstaller>();
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.GetPendingProjectFeeds().Returns([new NuGetSource { Name = "test", Url = "/feed", IsProjectFeed = true }]);
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new PluginInstallCommand(
            NullLogger<PluginInstallCommand>.Instance,
            installer,
            Substitute.For<IPackageIndexService>(),
            globalConfig,
            new ProjectFeedConsent(sourceManager, NonInteractive())).Create();

        var exitCode = await InvokeAsync(command, ["Fixture"]);

        Assert.AreEqual(1, exitCode);
        Assert.IsEmpty(installer.ReceivedCalls());
        Assert.IsEmpty(globalConfig.ReceivedCalls());
    }

    private static ProjectFeedConsent NoProjectFeeds()
    {
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.GetPendingProjectFeeds().Returns([]);
        return new ProjectFeedConsent(sourceManager, NonInteractive());
    }

    private static IConsoleCapabilities NonInteractive()
    {
        var capabilities = Substitute.For<IConsoleCapabilities>();
        capabilities.IsInteractive.Returns(false);
        return capabilities;
    }

    private static async Task<int> InvokeAsync(System.CommandLine.Command command, string[] args)
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
        AnsiConsole.Console = console;
        try
        {
            var parseResult = command.Parse(args);
            Assert.IsEmpty(parseResult.Errors);
            return await parseResult.InvokeAsync();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
