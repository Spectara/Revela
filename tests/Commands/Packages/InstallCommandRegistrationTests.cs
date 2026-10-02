using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Commands.Plugins;
using Spectara.Revela.Features.Theme.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Packages;

/// <summary>
/// Install and uninstall commands share one flow: the package type comes from the nuspec, and the exact
/// installed version is declared in project.json inside a project, otherwise in revela.json.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class InstallCommandRegistrationTests
{
    private const string PluginId = "Spectara.Revela.Plugins.Fixture";
    private const string ThemeId = "Spectara.Revela.Themes.Noir";

    private string pluginDirectory = null!;

    [TestInitialize]
    public void Initialize() => pluginDirectory = Directory.CreateTempSubdirectory("revela-install-").FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(pluginDirectory, recursive: true);

    [TestMethod]
    [DataRow(null)]
    [DataRow("latest")]
    public async Task PluginInstall_WithoutExactVersion_DeclaresInstalledVersion(string? requested)
    {
        var fixture = CreateFixture(insideProject: false);
        fixture.Installer.InstallAsync(PluginId, PackageIds.PluginPackageType, requested, null, Arg.Any<CancellationToken>())
            .Returns(Installed(PluginId, "1.4.2", PackageIds.PluginPackageType));

        var exitCode = await InvokeAsync(fixture.PluginInstall(), requested is null ? ["Fixture"] : ["Fixture", "--version", requested]);

        Assert.AreEqual(0, exitCode);
        await fixture.GlobalConfig.Received(1).AddPackageAsync(PluginId, "1.4.2", Arg.Any<CancellationToken>());
        await fixture.GlobalConfig.DidNotReceive().AddPackageAsync(Arg.Any<string>(), "latest", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task PluginInstall_InsideProject_DeclaresOnlyInProjectJson()
    {
        var fixture = CreateFixture(insideProject: true);
        fixture.Installer.InstallAsync(PluginId, PackageIds.PluginPackageType, null, null, Arg.Any<CancellationToken>())
            .Returns(Installed(PluginId, "1.4.2", PackageIds.PluginPackageType));

        var exitCode = await InvokeAsync(fixture.PluginInstall(), ["Fixture"]);

        Assert.AreEqual(0, exitCode);
        await fixture.ConfigService.Received(1).UpdateProjectConfigAsync(
            Arg.Is<JsonObject>(patch => DeclaredVersion(patch, PluginId) == "1.4.2"), Arg.Any<CancellationToken>());
        await fixture.GlobalConfig.DidNotReceive().AddPackageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ThemeInstall_InsideProjectWithoutPackageIndex_InstallsAndDeclaresOnlyInProjectJson()
    {
        var fixture = CreateFixture(insideProject: true);
        fixture.Installer.InstallAsync(ThemeId, PackageIds.ThemePackageType, null, null, Arg.Any<CancellationToken>())
            .Returns(Installed(ThemeId, "3.1.0-beta.2", PackageIds.ThemePackageType));

        var exitCode = await InvokeAsync(fixture.ThemeInstall(), ["Noir"]);

        Assert.AreEqual(0, exitCode);
        await fixture.ConfigService.Received(1).UpdateProjectConfigAsync(
            Arg.Is<JsonObject>(patch => DeclaredVersion(patch, ThemeId) == "3.1.0-beta.2"), Arg.Any<CancellationToken>());
        await fixture.GlobalConfig.DidNotReceive().AddPackageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.IsEmpty(fixture.Index.ReceivedCalls());
    }

    [TestMethod]
    public async Task ThemeInstall_PackageIsPlugin_FailsWithoutTouchingFiles()
    {
        var fixture = CreateFixture(insideProject: false);
        fixture.Installer.InstallAsync(PluginId, PackageIds.ThemePackageType, null, null, Arg.Any<CancellationToken>())
            .Returns(WrongType(PluginId, PackageIds.PluginPackageType));

        var (exitCode, output) = await InvokeWithOutputAsync(fixture.ThemeInstall(), [PluginId]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("is not a theme", output, StringComparison.Ordinal);
        Assert.Contains("revela plugin install", output, StringComparison.Ordinal);
        await fixture.Installer.DidNotReceive().UninstallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await fixture.GlobalConfig.DidNotReceive().AddPackageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task PluginInstall_PackageIsTheme_FailsWithoutTouchingFiles()
    {
        var fixture = CreateFixture(insideProject: false);
        fixture.Installer.InstallAsync(ThemeId, PackageIds.PluginPackageType, null, null, Arg.Any<CancellationToken>())
            .Returns(WrongType(ThemeId, PackageIds.ThemePackageType));

        var (exitCode, output) = await InvokeWithOutputAsync(fixture.PluginInstall(), [ThemeId]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("is not a plugin", output, StringComparison.Ordinal);
        Assert.Contains("revela theme install", output, StringComparison.Ordinal);
        await fixture.Installer.DidNotReceive().UninstallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await fixture.GlobalConfig.DidNotReceive().AddPackageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task PluginInstall_DeclarationFails_ReportsItAndKeepsInstalledFiles()
    {
        var fixture = CreateFixture(insideProject: true);
        fixture.Installer.InstallAsync(PluginId, PackageIds.PluginPackageType, null, null, Arg.Any<CancellationToken>())
            .Returns(Installed(PluginId, "1.4.2", PackageIds.PluginPackageType));
        fixture.ConfigService.UpdateProjectConfigAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Injected write failure.")));

        var (exitCode, output) = await InvokeWithOutputAsync(fixture.PluginInstall(), ["Fixture"]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("was installed, but declaring it", output, StringComparison.Ordinal);
        Assert.Contains("Injected write failure.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("installed successfully", output, StringComparison.Ordinal);
        await fixture.Installer.DidNotReceive().UninstallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task PluginUninstall_InsideProject_RemovesDeclarationOnlyFromProjectJson()
    {
        var fixture = CreateFixture(insideProject: true);
        fixture.Installer.UninstallAsync(PluginId, Arg.Any<CancellationToken>()).Returns(true);

        var exitCode = await InvokeAsync(fixture.PluginUninstall(), ["Fixture", "--yes"]);

        Assert.AreEqual(0, exitCode);
        await fixture.ConfigService.Received(1).UpdateProjectConfigAsync(
            Arg.Is<JsonObject>(patch => IsRemoval(patch, PluginId)), Arg.Any<CancellationToken>());
        await fixture.GlobalConfig.DidNotReceive().RemovePackageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ThemeUninstall_OutsideProject_RemovesDeclarationOnlyFromRevelaJson()
    {
        var fixture = CreateFixture(insideProject: false);
        fixture.Installer.UninstallAsync(ThemeId, Arg.Any<CancellationToken>()).Returns(true);

        var exitCode = await InvokeAsync(fixture.ThemeUninstall(), ["Noir", "--yes"]);

        Assert.AreEqual(0, exitCode);
        await fixture.GlobalConfig.Received(1).RemovePackageAsync(ThemeId, Arg.Any<CancellationToken>());
        await fixture.ConfigService.DidNotReceive().UpdateProjectConfigAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task PluginInstall_NoNameNonInteractive_FailsWithHintAndInstallsNothing()
    {
        var fixture = CreateFixture(insideProject: false);

        var (exitCode, output) = await InvokeWithOutputAsync(fixture.PluginInstall(), []);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela plugin install <name>", output, StringComparison.Ordinal);
        Assert.Contains("revela plugin install --all", output, StringComparison.Ordinal);
        Assert.IsEmpty(fixture.Installer.ReceivedCalls());
        Assert.IsEmpty(fixture.Index.ReceivedCalls());
    }

    [TestMethod]
    public async Task ThemeInstall_NoNameNonInteractive_FailsWithHintAndInstallsNothing()
    {
        var fixture = CreateFixture(insideProject: false);

        var (exitCode, output) = await InvokeWithOutputAsync(fixture.ThemeInstall(), []);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela theme install <name>", output, StringComparison.Ordinal);
        Assert.Contains("revela theme install --all", output, StringComparison.Ordinal);
        Assert.IsEmpty(fixture.Installer.ReceivedCalls());
        Assert.IsEmpty(fixture.Index.ReceivedCalls());
    }

    [TestMethod]
    public async Task PluginUninstall_WithoutYesNonInteractive_FailsWithHintAndKeepsPackage()
    {
        var fixture = CreateFixture(insideProject: false);

        var (exitCode, output) = await InvokeWithOutputAsync(fixture.PluginUninstall(), ["Fixture"]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela plugin uninstall Fixture --yes", output, StringComparison.Ordinal);
        Assert.IsEmpty(fixture.Installer.ReceivedCalls());
    }

    [TestMethod]
    public async Task ThemeUninstall_WithoutYesNonInteractive_FailsWithHintAndKeepsPackage()
    {
        var fixture = CreateFixture(insideProject: false);

        var (exitCode, output) = await InvokeWithOutputAsync(fixture.ThemeUninstall(), ["Noir"]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela theme uninstall Noir --yes", output, StringComparison.Ordinal);
        Assert.IsEmpty(fixture.Installer.ReceivedCalls());
    }

    [TestMethod]
    public async Task PluginInstall_ProjectFeedWithoutConsent_InstallsNothing()
    {
        var fixture = CreateFixture(insideProject: false);
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.GetPendingProjectFeeds().Returns([new NuGetSource { Name = "test", Url = "/feed", IsProjectFeed = true }]);
        var command = new PluginInstallCommand(
            NullLogger<PluginInstallCommand>.Instance,
            fixture.InstallService,
            fixture.Index,
            new ProjectFeedConsent(sourceManager, NonInteractive()),
            NonInteractive()).Create();

        var exitCode = await InvokeAsync(command, ["Fixture"]);

        Assert.AreEqual(1, exitCode);
        Assert.IsEmpty(fixture.Installer.ReceivedCalls());
        Assert.IsEmpty(fixture.GlobalConfig.ReceivedCalls());
    }

    private Fixture CreateFixture(bool insideProject)
    {
        var installer = Substitute.For<IPackageInstaller>();
        var configService = Substitute.For<IConfigService>();
        configService.IsProjectInitialized().Returns(insideProject);
        configService.ProjectConfigPath.Returns(Path.Combine(pluginDirectory, "project.json"));
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        globalConfig.ConfigFilePath.Returns(Path.Combine(pluginDirectory, "revela.json"));
        var declarations = new PackageDeclarations(configService, globalConfig, NullLogger<PackageDeclarations>.Instance);
        var installService = new PackageInstallService([installer], declarations, pluginDirectory);
        return new Fixture(installer, configService, globalConfig, Substitute.For<IPackageIndexService>(), installService);
    }

    private static PackageInstallResult Installed(string id, string version, string packageType) =>
        new(PackageInstallStatus.Installed, new InstalledPackage(id, version, [packageType]));

    private static PackageInstallResult WrongType(string id, string declaredPackageType) =>
        new(PackageInstallStatus.WrongPackageType, new InstalledPackage(id, "1.0.0", [declaredPackageType]));

    private static string? DeclaredVersion(JsonObject patch, string packageId) =>
        patch["dependencies"]?["packages"]?[packageId]?.GetValue<string>();

    private static bool IsRemoval(JsonObject patch, string packageId) =>
        patch["dependencies"]?["packages"] is JsonObject packages
        && packages.ContainsKey(packageId)
        && packages[packageId] is null;

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

    private static async Task<int> InvokeAsync(System.CommandLine.Command command, string[] args) =>
        (await InvokeWithOutputAsync(command, args)).ExitCode;

    private static async Task<(int ExitCode, string Output)> InvokeWithOutputAsync(System.CommandLine.Command command, string[] args)
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
        console.Profile.Width = 240;
        AnsiConsole.Console = console;
        try
        {
            var parseResult = command.Parse(args);
            Assert.IsEmpty(parseResult.Errors);
            var exitCode = await parseResult.InvokeAsync();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed record Fixture(
        IPackageInstaller Installer,
        IConfigService ConfigService,
        IGlobalConfigManager GlobalConfig,
        IPackageIndexService Index,
        PackageInstallService InstallService)
    {
        public System.CommandLine.Command PluginInstall() => new PluginInstallCommand(
            NullLogger<PluginInstallCommand>.Instance, InstallService, Index, NoProjectFeeds(), NonInteractive()).Create();

        public System.CommandLine.Command ThemeInstall() => new ThemeInstallCommand(
            NullLogger<ThemeInstallCommand>.Instance, Index, InstallService, [NoProjectFeeds()], NonInteractive()).Create();

        public System.CommandLine.Command PluginUninstall() => new PluginUninstallCommand(
            NullLogger<PluginUninstallCommand>.Instance, InstallService, NonInteractive()).Create();

        public System.CommandLine.Command ThemeUninstall() => new ThemeUninstallCommand(
            NullLogger<ThemeUninstallCommand>.Instance, InstallService, NonInteractive()).Create();
    }
}
