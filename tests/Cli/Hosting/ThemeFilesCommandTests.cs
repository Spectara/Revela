using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectre.Console;

namespace Spectara.Revela.Tests.Cli.Hosting;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class ThemeFilesCommandTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ThemeFiles_PackagedThemeWithoutLocalOverride_ListsFiles(bool explicitTheme)
    {
        using var project = TestProject.Create(project => project
            .WithProjectJson(new { project = new { name = "Theme files" }, theme = new { name = "Packaged" } }));
        var args = explicitTheme
            ? new[] { "theme", "files", "--theme", "Packaged" }
            : ["theme", "files"];

        var (exitCode, output) = await RunCliAsync(project.RootPath, args);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Layout.revela", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("probe.css", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Theme Not Found", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ThemeFiles_UnknownTheme_ReturnsFailure()
    {
        using var project = TestProject.Create(project => project
            .WithProjectJson(new { project = new { name = "Missing theme" } }));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["theme", "files", "--theme", "Missing"]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("Theme Not Found", output, StringComparison.Ordinal);
        Assert.Contains("Missing", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("theme", "install")]
    [DataRow("theme", "uninstall")]
    [DataRow("plugin", "install")]
    [DataRow("plugin", "uninstall")]
    public void AddPackages_MutatingCommand_DoesNotLoadAssemblies(string command, string subcommand)
    {
        var source = Substitute.For<IPackageSource>();
        var services = new ServiceCollection();

        services.AddPackages(source, new ConfigurationBuilder(), [command, subcommand]);

        source.DidNotReceive().LoadPlugins();
        source.DidNotReceive().LoadThemes();
    }

    private static async Task<(int ExitCode, string Output)> RunCliAsync(string projectPath, string[] args)
    {
        var theme = Substitute.For<ITheme>();
        theme.Metadata.Returns(new PackageMetadata
        {
            Id = "Tests.Themes.Packaged",
            Name = "Packaged",
            Version = "1.0.0",
            Description = "Packaged theme fixture"
        });
        theme.Prefix.Returns((string?)null);
        theme.TargetTheme.Returns((string?)null);
        theme.Manifest.Returns(new ThemeManifest { LayoutTemplate = "Layout.revela" });
        theme.GetAllFiles().Returns(["Layout.revela", "Assets/probe.css"]);
        var source = Substitute.For<IPackageSource>();
        source.LoadPlugins().Returns([]);
        source.LoadThemes().Returns([new LoadedThemeInfo(theme, PackageSource.Bundled)]);

        var originalConsole = AnsiConsole.Console;
        using var writer = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });

        try
        {
            var builder = HostBootstrap.CreateBuilder(args, source, projectPath);
            using var host = builder.Build();

            var exitCode = await host.RunRevelaAsync(args);

            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
