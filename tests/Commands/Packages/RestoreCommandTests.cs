using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Commands;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
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
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ExecuteAsync_InvalidThemes_InspectsRemainingDependenciesAndBlocksInstallation(bool checkOnly, bool includeMissing)
    {
        using var project = TestProject.CreateMinimal();
        var registry = Substitute.For<IThemeRegistry>();
        var dependencies = new List<RequiredDependency>
        {
            Theme("[Broken]"),
            Theme("[Valid]"),
            Theme("[AlsoBroken]")
        };
        var firstError = "Failed to load local theme '[Broken]': invalid [manifest].";
        var secondError = "Failed to load local theme '[AlsoBroken]': invalid [configuration].";
        registry.Resolve("[Broken]", project.RootPath).Returns(_ => throw new InvalidOperationException(firstError));
        registry.Resolve("[Valid]", project.RootPath).Returns(Substitute.For<ITheme>());
        registry.Resolve("[AlsoBroken]", project.RootPath).Returns(_ => throw new InvalidOperationException(secondError));
        if (includeMissing)
        {
            registry.Resolve("[Missing]", project.RootPath).Returns((ITheme?)null);
            dependencies.Add(Theme("[Missing]"));
            dependencies.Add(new RequiredDependency
            {
                PackageId = "Spectara.Revela.Plugins.[MissingPlugin]",
                Type = DependencyType.Plugin
            });
        }

        var (exitCode, output) = await InvokeAsync(project, dependencies, registry, checkOnly);

        Assert.AreEqual(1, exitCode);
        registry.Received(1).Resolve("[Broken]", project.RootPath);
        registry.Received(1).Resolve("[Valid]", project.RootPath);
        registry.Received(1).Resolve("[AlsoBroken]", project.RootPath);
        Assert.Contains("Theme [Broken] - invalid", output, StringComparison.Ordinal);
        Assert.Contains(firstError, output, StringComparison.Ordinal);
        Assert.Contains(secondError, output, StringComparison.Ordinal);
        Assert.Contains("Theme [Valid]", output, StringComparison.Ordinal);
        var missingCount = includeMissing ? 2 : 0;
        Assert.Contains($"2 theme(s) invalid; {missingCount} dependency(ies) missing.", output, StringComparison.Ordinal);
        Assert.Contains("Fix invalid local theme configuration", output, StringComparison.Ordinal);
        Assert.Contains("No packages were installed.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Installing ", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Run revela restore to install them.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("System.InvalidOperationException", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
        if (includeMissing)
        {
            registry.Received(1).Resolve("[Missing]", project.RootPath);
            Assert.Contains("Theme [Missing] - missing", output, StringComparison.Ordinal);
            Assert.Contains("Plugin Spectara.Revela.Plugins.[MissingPlugin] - missing", output, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_AllDependenciesInstalled_ReturnsSuccess(bool checkOnly)
    {
        using var project = TestProject.CreateMinimal();
        var registry = Substitute.For<IThemeRegistry>();
        registry.Resolve("[Valid]", project.RootPath).Returns(Substitute.For<ITheme>());

        var (exitCode, output) = await InvokeAsync(project, [Theme("[Valid]")], registry, checkOnly);

        Assert.AreEqual(0, exitCode);
        registry.Received(1).Resolve("[Valid]", project.RootPath);
        Assert.Contains("Theme [Valid]", output, StringComparison.Ordinal);
        Assert.Contains("All 1 dependency(ies) are installed.", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_CheckWithMissingDependency_ReturnsFailureAndRestoreGuidance()
    {
        using var project = TestProject.CreateMinimal();
        var registry = Substitute.For<IThemeRegistry>();
        registry.Resolve("[Missing]", project.RootPath).Returns((ITheme?)null);

        var (exitCode, output) = await InvokeAsync(project, [Theme("[Missing]")], registry, checkOnly: true);

        Assert.AreEqual(1, exitCode);
        registry.Received(1).Resolve("[Missing]", project.RootPath);
        Assert.Contains("Theme [Missing] - missing", output, StringComparison.Ordinal);
        Assert.Contains("1 dependency(ies) missing.", output, StringComparison.Ordinal);
        Assert.Contains("Run revela restore to install them.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("plugins:Spectara.Revela.Plugins.Missing", "1.0.0", "Plugin Spectara.Revela.Plugins.Missing")]
    [DataRow("themes:Spectara.Revela.Themes.Missing", "1.0.0", "Theme Missing")]
    [DataRow("theme:name", "Missing", "Theme Missing")]
    [DataRow("theme:name", "Spectara.Revela.Themes.Missing", "Theme Missing")]
    public async Task Create_CheckWithConfiguredMissingDependency_ReturnsFailure(string key, string value, string label)
    {
        using var project = TestProject.CreateMinimal();
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();
        using var provider = CreateScannerProvider(configuration);
        var scanner = provider.GetRequiredService<IDependencyScanner>();
        var registry = Substitute.For<IThemeRegistry>();
        registry.Resolve("Missing", project.RootPath).Returns((ITheme?)null);

        var (exitCode, output) = await InvokeAsync(project, scanner, registry, checkOnly: true);

        Assert.AreEqual(1, exitCode);
        Assert.Contains($"{label} - missing", output, StringComparison.Ordinal);
        Assert.Contains("1 dependency(ies) missing.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("No dependencies to restore.", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("Lumina")]
    [DataRow("Spectara.Revela.Themes.Lumina")]
    [DataRow("lumina")]
    public void GetDependencies_LayeredRootConfiguration_PreservesActiveVersionAndMergedPackages(string activeTheme)
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["theme:name"] = "Other",
                ["themes:Spectara.Revela.Themes.Lumina"] = "1.0.0",
                ["themes:Spectara.Revela.Themes.Other"] = "1.5.0",
                ["plugins:Spectara.Revela.Plugins.Statistics"] = "1.0.0"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["theme:name"] = activeTheme,
                ["themes:Spectara.Revela.Themes.Lumina"] = "2.0.0",
                ["plugins:Spectara.Revela.Plugins.Statistics"] = "3.0.0"
            })
            .Build();
        using var provider = CreateScannerProvider(configuration);

        var dependencies = provider.GetRequiredService<IDependencyScanner>().GetDependencies();

        Assert.HasCount(3, dependencies);
        var active = dependencies.Single(dependency => string.Equals(
            dependency.PackageId, "Spectara.Revela.Themes.Lumina", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(DependencyType.Theme, active.Type);
        Assert.AreEqual("2.0.0", active.Version);
        var other = dependencies.Single(dependency => string.Equals(
            dependency.PackageId, "Spectara.Revela.Themes.Other", StringComparison.Ordinal));
        Assert.AreEqual(DependencyType.Theme, other.Type);
        Assert.AreEqual("1.5.0", other.Version);
        var plugin = dependencies.Single(dependency => string.Equals(
            dependency.PackageId, "Spectara.Revela.Plugins.Statistics", StringComparison.Ordinal));
        Assert.AreEqual(DependencyType.Plugin, plugin.Type);
        Assert.AreEqual("3.0.0", plugin.Version);
    }

    [TestMethod]
    public async Task Create_CheckWithConfiguredInstalledTheme_ReturnsSuccessWithoutDuplicateDependencies()
    {
        using var project = TestProject.CreateMinimal();
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["theme:name"] = "Valid",
                ["themes:Spectara.Revela.Themes.Valid"] = "2.0.0"
            })
            .Build();
        using var provider = CreateScannerProvider(configuration);
        var registry = Substitute.For<IThemeRegistry>();
        registry.Resolve("Valid", project.RootPath).Returns(Substitute.For<ITheme>());

        var (exitCode, output) = await InvokeAsync(
            project, provider.GetRequiredService<IDependencyScanner>(), registry, checkOnly: true);

        Assert.AreEqual(0, exitCode);
        registry.Received(1).Resolve("Valid", project.RootPath);
        Assert.Contains("All 1 dependency(ies) are installed.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("No dependencies to restore.", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("Spectara.Revela.Plugins.Calendar", "Calendar", 1)]
    [DataRow("Spectara.Revela.Plugins.Other", "Source.Calendar", 1)]
    [DataRow("Spectara.Revela.Plugins.Source.Calendar", "Booking Feed", 0)]
    [DataRow("spectara.revela.plugins.source.calendar", "Booking Feed", 0)]
    public async Task Create_CheckWithConfiguredPlugin_MatchesExactPackageId(string installedId, string displayName, int expectedExitCode)
    {
        using var project = TestProject.CreateMinimal();
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["plugins:Spectara.Revela.Plugins.Source.Calendar"] = "2.0.0"
            })
            .Build();
        using var provider = CreateScannerProvider(configuration);
        var scanner = provider.GetRequiredService<IDependencyScanner>();
        var plugin = Substitute.For<IPlugin>();
        plugin.Metadata.Returns(new PackageMetadata
        {
            Id = installedId,
            Name = displayName,
            Version = "1.0.0",
            Description = "Installed plugin fixture"
        });
        var registry = new ThemeRegistry([], NullLogger<ThemeRegistry>.Instance);

        var (exitCode, output) = await InvokeAsync(project, scanner, registry, checkOnly: true, installedPlugins: [plugin]);

        Assert.AreEqual(expectedExitCode, exitCode);
        var dependency = scanner.GetDependencies().Single();
        Assert.AreEqual(DependencyType.Plugin, dependency.Type);
        Assert.AreEqual("2.0.0", dependency.Version);
        Assert.Contains(expectedExitCode == 0 ? "All 1 dependency(ies) are installed." : "1 dependency(ies) missing.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Installing ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("plugins", null, 1)]
    [DataRow("themes", null, 1)]
    [DataRow("plugins", "Spectara.Revela.Themes.Lumina.Statistics", 0)]
    [DataRow("themes", "Spectara.Revela.Themes.Lumina.Statistics", 0)]
    [DataRow("plugins", "spectara.revela.themes.lumina.statistics", 0)]
    [DataRow("plugins", "Spectara.Revela.Themes.Other.Statistics", 1)]
    [DataRow("themes", "Spectara.Revela.Themes.Other.Statistics", 1)]
    public async Task Create_CheckWithConfiguredThemeExtension_MatchesFullExtensionIdentity(string section, string? installedExtensionId, int expectedExitCode)
    {
        using var project = TestProject.CreateMinimal();
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{section}:Spectara.Revela.Themes.Lumina.Statistics"] = "2.0.0"
            })
            .Build();
        using var provider = CreateScannerProvider(configuration);
        var scanner = provider.GetRequiredService<IDependencyScanner>();
        var installedThemes = new List<ITheme>
        {
            InstalledTheme("Spectara.Revela.Themes.Lumina", "Lumina"),
            InstalledTheme("Spectara.Revela.Themes.Statistics", "Statistics")
        };
        if (installedExtensionId is not null)
        {
            var extension = InstalledTheme(installedExtensionId, "Reports");
            extension.Prefix.Returns("statistics");
            extension.TargetTheme.Returns("Lumina");
            installedThemes.Add(extension);
        }

        var registry = new ThemeRegistry(installedThemes, NullLogger<ThemeRegistry>.Instance);

        var (exitCode, output) = await InvokeAsync(project, scanner, registry, checkOnly: true);

        Assert.AreEqual(expectedExitCode, exitCode);
        var dependency = scanner.GetDependencies().Single();
        Assert.AreEqual("Spectara.Revela.Themes.Lumina.Statistics", dependency.PackageId);
        Assert.AreEqual(DependencyType.Theme, dependency.Type);
        Assert.AreEqual("2.0.0", dependency.Version);
        Assert.Contains("Theme Lumina.Statistics", output, StringComparison.Ordinal);
        Assert.Contains(expectedExitCode == 0 ? "All 1 dependency(ies) are installed." : "1 dependency(ies) missing.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("No dependencies to restore.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Installing ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Create_CheckWithUnknownPackagePrefixes_IgnoresDependencies()
    {
        using var project = TestProject.CreateMinimal();
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["plugins:Other.Plugins.Calendar"] = "1.0.0",
                ["themes:Other.Themes.Lumina"] = "1.0.0"
            })
            .Build();
        using var provider = CreateScannerProvider(configuration);
        var scanner = provider.GetRequiredService<IDependencyScanner>();
        var registry = new ThemeRegistry([], NullLogger<ThemeRegistry>.Instance);

        var (exitCode, output) = await InvokeAsync(project, scanner, registry, checkOnly: true);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(scanner.GetDependencies());
        Assert.Contains("No dependencies to restore.", output, StringComparison.Ordinal);
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

    private static RequiredDependency Theme(string name) => new()
    {
        PackageId = $"Spectara.Revela.Themes.{name}",
        Type = DependencyType.Theme
    };

    private static async Task<(int ExitCode, string Output)> InvokeAsync(
        TestProject project,
        IReadOnlyList<RequiredDependency> dependencies,
        IThemeRegistry registry,
        bool checkOnly)
    {
        var scanner = Substitute.For<IDependencyScanner>();
        scanner.GetDependencies().Returns(dependencies);
        var result = await InvokeAsync(project, scanner, registry, checkOnly);
        scanner.Received(1).GetDependencies();
        return result;
    }

    private static async Task<(int ExitCode, string Output)> InvokeAsync(
        TestProject project,
        IDependencyScanner scanner,
        IThemeRegistry registry,
        bool checkOnly,
        IEnumerable<IPlugin>? installedPlugins = null)
    {
        var environment = Options.Create(new ProjectEnvironment { Path = project.RootPath });
        var packageLogger = Substitute.For<ILogger<PackageManager>>();
        var sourceManager = Substitute.For<INuGetSourceManager>();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var services = new ServiceCollection();
        services.AddSingleton(new NupkgExtractor(NullLogger<NupkgExtractor>.Instance, TimeProvider.System));
        services.AddSingleton(new PluginProjectService(host.Services.GetRequiredService<IConfigService>(), NullLogger<PluginProjectService>.Instance));
        services.AddSingleton(packageLogger);
        services.AddSingleton(sourceManager);
        services.AddSingleton(Substitute.For<IBuildInfo>());
        services.AddHttpClient<PackageManager>()
            .ConfigurePrimaryHttpMessageHandler(() => new RejectingHttpMessageHandler());
        using var provider = services.BuildServiceProvider();
        var command = new RestoreCommand(
            scanner,
            registry,
            installedPlugins ?? [],
            provider.GetRequiredService<PackageManager>(),
            environment,
            NullLogger<RestoreCommand>.Instance).Create();
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
            var parseResult = command.Parse(checkOnly ? ["--check"] : []);
            Assert.IsEmpty(parseResult.Errors);
            var exitCode = await parseResult.InvokeAsync();

            Assert.IsEmpty(packageLogger.ReceivedCalls(), "Restore must not invoke the package manager.");
            Assert.IsEmpty(sourceManager.ReceivedCalls(), "Restore must not query package sources.");
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed class RejectingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Network access is forbidden in restore command tests.");
    }
}
