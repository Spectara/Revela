using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Commands.Restore;
using Spectara.Revela.Features.Packages.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class RestoreCommandTests
{
    #region Dependency scanner

    [TestMethod]
    public void GetDependencies_LayeredConfiguration_MergesPackagesPerKeyWithoutClassifyingIds()
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["dependencies:packages:Spectara.Revela.Themes.Lumina"] = "1.0.0",
                ["dependencies:packages:Spectara.Revela.Plugins.Statistics"] = "1.0.0"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["dependencies:packages:Spectara.Revela.Themes.Lumina"] = "2.0.0",
                ["dependencies:packages:Acme.Revela.Watermark"] = "3.0.0"
            })
            .Build();
        using var provider = CreateScannerProvider(configuration);

        var dependencies = provider.GetRequiredService<IDependencyScanner>().GetDependencies()
            .ToDictionary(d => d.PackageId, d => d.Version, StringComparer.Ordinal);

        Assert.HasCount(3, dependencies);
        Assert.AreEqual("2.0.0", dependencies["Spectara.Revela.Themes.Lumina"]);
        Assert.AreEqual("1.0.0", dependencies["Spectara.Revela.Plugins.Statistics"]);
        Assert.AreEqual("3.0.0", dependencies["Acme.Revela.Watermark"]);
    }

    [TestMethod]
    [DataRow("plugins:Spectara.Revela.Plugins.Serve")]
    [DataRow("themes:Spectara.Revela.Themes.Lumina")]
    [DataRow("plugins:serve:port")]
    public void GetDependencies_LegacyRootMaps_AreNotReadAsDependencies(string key)
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = "1.0.0" })
            .Build();
        using var provider = CreateScannerProvider(configuration);

        Assert.IsEmpty(provider.GetRequiredService<IDependencyScanner>().GetDependencies());
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("  ", null)]
    [DataRow("Noir", "Noir")]
    public void GetActiveThemeName_ReturnsConfiguredManifestName(string? configured, string? expected)
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["theme:name"] = configured })
            .Build();
        using var provider = CreateScannerProvider(configuration);

        Assert.AreEqual(expected, provider.GetRequiredService<IDependencyScanner>().GetActiveThemeName());
    }

    #endregion

    #region Check

    [TestMethod]
    public async Task Check_NothingDeclared_ReportsNoDependencies()
    {
        using var project = TestProject.CreateMinimal();

        var (exitCode, output) = await InvokeAsync(project, Scanner(), EmptyRegistry(), ["--check"]);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("No dependencies to restore.", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Check_ThirdPartyPackageNotInstalled_IsReportedMissing()
    {
        using var project = TestProject.CreateMinimal();

        var (exitCode, output) = await InvokeAsync(
            project, Scanner(packages: [("Acme.Revela.Watermark", "1.0.0")]), EmptyRegistry(), ["--check"]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("Package Acme.Revela.Watermark - missing", output, StringComparison.Ordinal);
        Assert.Contains("1 dependency(ies) missing.", output, StringComparison.Ordinal);
        Assert.Contains("Run revela restore to install them.", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("Spectara.Revela.Plugins.Source.Calendar", 0)]
    [DataRow("spectara.revela.plugins.source.calendar", 0)]
    [DataRow("Spectara.Revela.Plugins.Calendar", 1)]
    public async Task Check_InstalledPlugin_MatchesExactPackageId(string installedId, int expectedExitCode)
    {
        using var project = TestProject.CreateMinimal();

        var (exitCode, output) = await InvokeAsync(
            project,
            Scanner(packages: [("Spectara.Revela.Plugins.Source.Calendar", "2.0.0")]),
            EmptyRegistry(),
            ["--check"],
            installedPlugins: [InstalledPlugin(installedId)]);

        Assert.AreEqual(expectedExitCode, exitCode);
        Assert.Contains(expectedExitCode == 0 ? "All 1 dependency(ies) are installed." : "1 dependency(ies) missing.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Installing ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("Spectara.Revela.Themes.Lumina.Statistics", 0)]
    [DataRow("Spectara.Revela.Themes.Other.Statistics", 1)]
    public async Task Check_InstalledThemeExtension_MatchesByPackageIdAcrossThemes(string installedExtensionId, int expectedExitCode)
    {
        using var project = TestProject.CreateMinimal();
        var extension = InstalledTheme(installedExtensionId, "Reports");
        extension.Prefix.Returns("statistics");
        extension.TargetTheme.Returns("Lumina");
        List<ITheme> themes = [InstalledTheme("Spectara.Revela.Themes.Lumina", "Lumina"), extension];

        var (exitCode, output) = await InvokeAsync(
            project,
            Scanner(packages: [("Spectara.Revela.Themes.Lumina.Statistics", "2.0.0")]),
            new ThemeRegistry(themes, NullLogger<ThemeRegistry>.Instance),
            ["--check"],
            installedThemes: themes);

        Assert.AreEqual(expectedExitCode, exitCode);
        if (expectedExitCode == 0)
        {
            Assert.Contains("Theme Spectara.Revela.Themes.Lumina.Statistics", output, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("Package Spectara.Revela.Themes.Lumina.Statistics - missing", output, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task Check_ActiveThemeByManifestName_UsesInstalledPackageIdInsteadOfOfficialPrefix()
    {
        using var project = TestProject.CreateMinimal();
        List<ITheme> themes = [InstalledTheme("Acme.Revela.Noir", "Noir")];

        var (exitCode, output) = await InvokeAsync(
            project,
            Scanner(activeTheme: "Noir"),
            new ThemeRegistry(themes, NullLogger<ThemeRegistry>.Instance),
            ["--check"],
            installedThemes: themes);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Theme Noir (Acme.Revela.Noir)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Spectara.Revela.Themes.Noir", output, StringComparison.Ordinal);
        Assert.Contains("All 1 dependency(ies) are installed.", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Check_ActiveThemeNotProvided_IsReportedMissing()
    {
        using var project = TestProject.CreateMinimal();

        var (exitCode, output) = await InvokeAsync(project, Scanner(activeTheme: "Noir"), EmptyRegistry(), ["--check"]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("Theme Noir - missing (no installed or local theme has this name)", output, StringComparison.Ordinal);
        Assert.Contains("1 dependency(ies) missing.", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_InvalidLocalActiveTheme_BlocksInstallation(bool checkOnly)
    {
        using var project = TestProject.CreateMinimal();
        var registry = Substitute.For<IThemeRegistry>();
        const string error = "Local theme manifest 'themes/[Broken]/theme.json' could not be loaded: invalid [manifest].";
        registry.Resolve("[Broken]", project.RootPath).Returns(_ => throw new InvalidOperationException(error));

        var (exitCode, output) = await InvokeAsync(
            project,
            Scanner(packages: [("Spectara.Revela.Plugins.Missing", "1.0.0")], activeTheme: "[Broken]"),
            registry,
            checkOnly ? ["--check"] : []);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("Theme [Broken] - invalid", output, StringComparison.Ordinal);
        Assert.Contains(error, output, StringComparison.Ordinal);
        Assert.Contains("1 theme(s) invalid; 1 dependency(ies) missing.", output, StringComparison.Ordinal);
        Assert.Contains("No packages were installed.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Installing ", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    #endregion

    #region Install

    [TestMethod]
    public async Task Restore_ProjectFeedWithoutConsentNonInteractive_FailsWithHintAndInstallsNothing()
    {
        using var project = TestProject.CreateMinimal();
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.ProjectConfigPath.Returns(project.ProjectJsonPath);
        sourceManager.GetPendingProjectFeeds().Returns([new NuGetSource { Name = "sneaky", Url = @"C:\sneaky-feed", IsProjectFeed = true }]);

        var (exitCode, output) = await InvokeAsync(
            project,
            Scanner(packages: [("Acme.Revela.Watermark", "1.0.0")]),
            EmptyRegistry(),
            [],
            sourceManager: sourceManager);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("sneaky", output, StringComparison.Ordinal);
        Assert.Contains(@"C:\sneaky-feed", output, StringComparison.Ordinal);
        Assert.Contains("project.json", output, StringComparison.Ordinal);
        Assert.Contains("--allow-project-feeds", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Installing ", output, StringComparison.Ordinal);
        sourceManager.DidNotReceive().ApproveProjectFeeds();
        _ = await sourceManager.DidNotReceive().LoadSourcesAsync(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Restore_ThirdPartyDependencyFromProjectFolderFeed_InstallsAndPinsExactVersion()
    {
        var packageId = $"Acme.Revela.Watermark{Guid.NewGuid():N}";
        var pluginPath = Path.Combine(PackageManager.PluginDirectory, packageId);
        using var project = TestProject.CreateMinimal();
        _ = TestPackageFactory.CreatePackage(Path.Combine(project.RootPath, "feed"), packageId, "1.2.0");
        await File.WriteAllTextAsync(project.ProjectJsonPath, $$"""
            {
              "project": { "name": "restore-test" },
              "dependencies": {
                "feeds": { "local": "./feed" },
                "packages": { "{{packageId}}": "latest" }
              }
            }
            """);
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        globalConfig.ConfigFilePath.Returns(Path.Combine(project.RootPath, "global", "revela.json"));
        var sourceManager = new OfflineSourceManager(new NuGetSourceManager(
            NullLogger<NuGetSourceManager>.Instance,
            host.Services.GetRequiredService<IOptionsMonitor<DependenciesConfig>>(),
            Options.Create(new ProjectEnvironment { Path = project.RootPath }),
            globalConfig));
        using var scannerProvider = CreateScannerProvider(configuration);

        try
        {
            var (exitCode, output) = await InvokeAsync(
                project,
                scannerProvider.GetRequiredService<IDependencyScanner>(),
                EmptyRegistry(),
                ["--allow-project-feeds"],
                sourceManager: sourceManager,
                configService: host.Services.GetRequiredService<IConfigService>());

            Assert.AreEqual(0, exitCode, output);
            Assert.Contains($"Package {packageId} - missing", output, StringComparison.Ordinal);
            Assert.Contains($"Plugin {packageId} 1.2.0", output, StringComparison.Ordinal);
            Assert.Contains("Restore complete", output, StringComparison.Ordinal);
            Assert.DoesNotContain("This project declares package feeds", output, StringComparison.Ordinal);
            Assert.IsTrue(File.Exists(Path.Combine(pluginPath, $"{packageId}.dll")));
            var saved = JsonNode.Parse(await File.ReadAllTextAsync(project.ProjectJsonPath))!;
            Assert.AreEqual("1.2.0", saved["dependencies"]!["packages"]![packageId]!.GetValue<string>());
            Assert.AreEqual("./feed", saved["dependencies"]!["feeds"]!["local"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task Restore_DependencyDeclaredOnlyGlobally_DoesNotPinItIntoProjectJson()
    {
        var packageId = $"Acme.Revela.GlobalOnly{Guid.NewGuid():N}";
        var pluginPath = Path.Combine(PackageManager.PluginDirectory, packageId);
        using var project = TestProject.CreateMinimal();
        var feed = Path.Combine(project.RootPath, "feed");
        _ = TestPackageFactory.CreatePackage(feed, packageId, "2.1.0");
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.GetPendingProjectFeeds().Returns([]);
        sourceManager.LoadSourcesAsync(Arg.Any<CancellationToken>()).Returns([new NuGetSource { Name = "local", Url = feed }]);

        try
        {
            var (exitCode, output) = await InvokeAsync(
                project, Scanner([(packageId, "latest")]), EmptyRegistry(), [], sourceManager: sourceManager);

            Assert.AreEqual(0, exitCode, output);
            Assert.Contains($"Plugin {packageId} 2.1.0", output, StringComparison.Ordinal);
            Assert.IsTrue(File.Exists(Path.Combine(pluginPath, $"{packageId}.dll")));
            CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        }
        finally
        {
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task Restore_ActiveThemeNotProvidedAnywhere_TriesOfficialFallbackAndExplainsFailure()
    {
        using var project = TestProject.CreateMinimal();
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.GetPendingProjectFeeds().Returns([]);
        sourceManager.LoadSourcesAsync(Arg.Any<CancellationToken>()).Returns([]);

        var (exitCode, output) = await InvokeAsync(
            project, Scanner(activeTheme: "Noir"), EmptyRegistry(), [], sourceManager: sourceManager);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("No installed theme or declared dependency provides theme Noir", output, StringComparison.Ordinal);
        Assert.Contains("Spectara.Revela.Themes.Noir", output, StringComparison.Ordinal);
        Assert.Contains("could not be resolved", output, StringComparison.Ordinal);
        Assert.Contains("dependencies.packages", output, StringComparison.Ordinal);
        _ = await sourceManager.Received(1).LoadSourcesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    private static IDependencyScanner Scanner(
        IReadOnlyList<(string Id, string? Version)>? packages = null,
        string? activeTheme = null)
    {
        var scanner = Substitute.For<IDependencyScanner>();
        scanner.GetDependencies().Returns(
            [.. (packages ?? []).Select(p => new RequiredDependency { PackageId = p.Id, Version = p.Version })]);
        scanner.GetActiveThemeName().Returns(activeTheme);
        return scanner;
    }

    private static ThemeRegistry EmptyRegistry() => new([], NullLogger<ThemeRegistry>.Instance);

    private static IPlugin InstalledPlugin(string packageId)
    {
        var plugin = Substitute.For<IPlugin>();
        plugin.Metadata.Returns(new PackageMetadata
        {
            Id = packageId,
            Name = "Installed plugin",
            Version = "1.0.0",
            Description = "Installed plugin fixture"
        });
        return plugin;
    }

    private static ITheme InstalledTheme(string packageId, string name)
    {
        var theme = Substitute.For<ITheme>();
        theme.Metadata.Returns(new PackageMetadata
        {
            Id = packageId,
            Name = name,
            Version = "1.0.0",
            Description = "Installed theme fixture"
        });
        theme.Prefix.Returns((string?)null);
        theme.TargetTheme.Returns((string?)null);
        return theme;
    }

    private static ServiceProvider CreateScannerProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddRevelaConfigSections();
        services.AddSingleton<IDependencyScanner, DependencyScanner>();
        return services.BuildServiceProvider();
    }

    private static async Task<(int ExitCode, string Output)> InvokeAsync(
        TestProject project,
        IDependencyScanner scanner,
        IThemeRegistry registry,
        string[] args,
        IEnumerable<IPlugin>? installedPlugins = null,
        IEnumerable<ITheme>? installedThemes = null,
        INuGetSourceManager? sourceManager = null,
        IConfigService? configService = null)
    {
        var environment = Options.Create(new ProjectEnvironment { Path = project.RootPath });
        var isolatedSources = sourceManager is null;
        sourceManager ??= Substitute.For<INuGetSourceManager>();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var services = new ServiceCollection();
        services.AddSingleton(new NupkgExtractor(NullLogger<NupkgExtractor>.Instance));
        services.AddSingleton<ILogger<PackageManager>>(NullLogger<PackageManager>.Instance);
        services.AddSingleton(sourceManager);
        services.AddSingleton(Substitute.For<IBuildInfo>());
        services.AddHttpClient<PackageManager>()
            .ConfigurePrimaryHttpMessageHandler(() => new RejectingHttpMessageHandler());
        using var provider = services.BuildServiceProvider();
        var packageManager = provider.GetRequiredService<PackageManager>();
        var declarations = new PackageDeclarations(
            configService ?? host.Services.GetRequiredService<IConfigService>(),
            Substitute.For<IGlobalConfigManager>(),
            NullLogger<PackageDeclarations>.Instance);
        var console = Substitute.For<IConsoleCapabilities>();
        console.IsInteractive.Returns(false);
        var command = new RestoreCommand(
            scanner,
            registry,
            installedPlugins ?? [],
            installedThemes ?? [],
            packageManager,
            new PackageInstallService([packageManager], declarations),
            declarations,
            new ProjectFeedConsent(sourceManager, console),
            environment,
            NullLogger<RestoreCommand>.Instance).Create();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var ansiConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        ansiConsole.Profile.Width = 400;
        AnsiConsole.Console = ansiConsole;

        try
        {
            var parseResult = command.Parse(args);
            Assert.IsEmpty(parseResult.Errors);
            var exitCode = await parseResult.InvokeAsync();

            var output = writer.ToString();
            if (isolatedSources && args.Contains("--check"))
            {
                Assert.IsEmpty(sourceManager.ReceivedCalls(), "restore --check must not query package sources.");
            }

            return (exitCode, output);
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    /// <summary>
    /// Real source manager without nuget.org, so restore tests never touch the network.
    /// </summary>
    private sealed class OfflineSourceManager(NuGetSourceManager inner) : INuGetSourceManager
    {
        public string? ProjectConfigPath => inner.ProjectConfigPath;

        public async Task<List<NuGetSource>> LoadSourcesAsync(CancellationToken cancellationToken = default) =>
            [.. (await inner.LoadSourcesAsync(cancellationToken)).Where(s => s.Name != "nuget.org")];

        public async Task<List<(NuGetSource Source, string Location)>> GetAllSourcesWithLocationAsync(CancellationToken cancellationToken = default) =>
            [.. (await inner.GetAllSourcesWithLocationAsync(cancellationToken)).Where(s => s.Source.Name != "nuget.org")];

        public IReadOnlyList<NuGetSource> GetProjectFeeds() => inner.GetProjectFeeds();

        public IReadOnlyList<NuGetSource> GetPendingProjectFeeds() => inner.GetPendingProjectFeeds();

        public void ApproveProjectFeeds() => inner.ApproveProjectFeeds();
    }

    private sealed class RejectingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Network access is forbidden in restore command tests.");
    }
}
