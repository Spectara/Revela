using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
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
        var environment = Options.Create(new ProjectEnvironment { Path = project.RootPath });
        var packageLogger = Substitute.For<ILogger<PackageManager>>();
        var sourceManager = Substitute.For<INuGetSourceManager>();
        var services = new ServiceCollection();
        services.AddSingleton(new NupkgExtractor(NullLogger<NupkgExtractor>.Instance, TimeProvider.System));
        services.AddSingleton(new PluginProjectService(environment, NullLogger<PluginProjectService>.Instance));
        services.AddSingleton(packageLogger);
        services.AddSingleton(sourceManager);
        services.AddHttpClient<PackageManager>()
            .ConfigurePrimaryHttpMessageHandler(() => new RejectingHttpMessageHandler());
        using var provider = services.BuildServiceProvider();
        var command = new RestoreCommand(
            scanner,
            registry,
            [],
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
            var exitCode = await command.Parse(checkOnly ? ["--check"] : []).InvokeAsync();

            scanner.Received(1).GetDependencies();
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
