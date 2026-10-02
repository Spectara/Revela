using System.CommandLine;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Theme.Commands;
using Spectara.Revela.Features.Theme.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Theme;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class ThemeExtractDeletionGuardTests
{
    private const string SourceTheme = "Lumina";

    [TestMethod]
    [DataRow("..")]
    [DataRow("../site/source")]
    [DataRow(".")]
    [DataRow("Custom/..")]
    [DataRow("ABSOLUTE")]
    public async Task ExtractAsync_TargetOutsideThemesDirectory_FailsAndDeletesNothing(string targetName)
    {
        using var workspace = TestProject.Create();
        var fixture = ThemeFixture.Create(workspace.RootPath);
        var resolvedTarget = fixture.ResolveTarget(targetName);

        var result = await fixture.CreateService().ExtractAsync(SourceTheme, resolvedTarget, force: true);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        fixture.AssertNothingDeleted();
    }

    [TestMethod]
    [DataRow("..")]
    [DataRow("../site/source")]
    [DataRow(".")]
    [DataRow("ABSOLUTE")]
    public async Task ExtractCommand_ForceWithTargetOutsideThemesDirectory_FailsAndDeletesNothing(string targetName)
    {
        using var workspace = TestProject.Create();
        var fixture = ThemeFixture.Create(workspace.RootPath);
        var command = fixture.CreateCommand().Create();

        var exitCode = await InvokeQuietAsync(command, [SourceTheme, fixture.ResolveTarget(targetName), "--force"]);

        Assert.AreEqual(1, exitCode);
        fixture.AssertNothingDeleted();
    }

    [TestMethod]
    public async Task ExtractAsync_ForceWithExistingThemeInsideThemesDirectory_ReplacesTheme()
    {
        using var workspace = TestProject.Create();
        var fixture = ThemeFixture.Create(workspace.RootPath);
        var customTheme = Path.Combine(fixture.ThemesPath, "Custom");
        var staleFile = Path.Combine(customTheme, "stale.txt");
        Directory.CreateDirectory(customTheme);
        await File.WriteAllTextAsync(staleFile, "old");

        var result = await fixture.CreateService().ExtractAsync(SourceTheme, "Custom", force: true);

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.IsFalse(File.Exists(staleFile));
        AssertLocalTheme(customTheme, "Custom");
        fixture.AssertNothingDeleted();
    }

    [TestMethod]
    public async Task ExtractCommand_ForceWithExistingThemeInsideThemesDirectory_ReplacesTheme()
    {
        using var workspace = TestProject.Create();
        var fixture = ThemeFixture.Create(workspace.RootPath);
        var customTheme = Path.Combine(fixture.ThemesPath, "Custom");
        var staleFile = Path.Combine(customTheme, "stale.txt");
        Directory.CreateDirectory(customTheme);
        await File.WriteAllTextAsync(staleFile, "old");
        var command = fixture.CreateCommand().Create();

        var exitCode = await InvokeQuietAsync(command, [SourceTheme, "Custom", "--force"]);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(File.Exists(staleFile));
        AssertLocalTheme(customTheme, "Custom");
        fixture.AssertNothingDeleted();
    }

    [TestMethod]
    public async Task ExtractCommand_SameName_WritesThemeJsonWithOriginalName()
    {
        using var workspace = TestProject.Create();
        var fixture = ThemeFixture.Create(workspace.RootPath);
        var command = fixture.CreateCommand().Create();

        var exitCode = await InvokeQuietAsync(command, [SourceTheme]);

        Assert.AreEqual(0, exitCode);
        AssertLocalTheme(Path.Combine(fixture.ThemesPath, SourceTheme), SourceTheme);
    }

    /// <summary>
    /// A full extraction must be recognised as a local theme: theme.json carrying the
    /// target name, and no leftover bundled manifest.json.
    /// </summary>
    private static void AssertLocalTheme(string themePath, string expectedName)
    {
        Assert.IsFalse(File.Exists(Path.Combine(themePath, "manifest.json")), "Bundled manifest.json must become theme.json");
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(themePath, "theme.json")))!;
        Assert.AreEqual(expectedName, manifest["name"]!.GetValue<string>());
        Assert.AreEqual("1.0.0", manifest["version"]!.GetValue<string>(), "Remaining manifest fields must be preserved");
    }

    private static async Task<int> InvokeQuietAsync(Command command, string[] args)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });

        try
        {
            return await command.Parse(args).InvokeAsync();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed class ThemeFixture
    {
        private readonly string photo;
        private readonly string projectJson;
        private readonly string otherTheme;
        private readonly string victim;
        private readonly IThemeRegistry registry;

        private ThemeFixture(string workspaceRoot)
        {
            ProjectPath = Path.Combine(workspaceRoot, "site");
            ThemesPath = Path.Combine(ProjectPath, ProjectPaths.Themes);
            projectJson = Path.Combine(ProjectPath, "project.json");
            photo = Path.Combine(ProjectPath, "source", "photo.jpg");
            otherTheme = Path.Combine(ThemesPath, "Other", "theme.json");
            victim = Path.Combine(workspaceRoot, "victim", "keep.txt");

            foreach (var file in new[] { projectJson, photo, otherTheme, victim })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, "data");
            }

            // Mirrors EmbeddedTheme: bundled themes extract their read-only manifest.json.
            var theme = Substitute.For<ITheme>();
            theme.ExtractToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var target = call.Arg<string>();
                Directory.CreateDirectory(target);
                return File.WriteAllTextAsync(Path.Combine(target, "manifest.json"), /*lang=json,strict*/ """{ "name": "Lumina", "version": "1.0.0" }""");
            });

            registry = Substitute.For<IThemeRegistry>();
            registry.ResolveInstalled(SourceTheme).Returns(theme);
            registry.GetExtensions(Arg.Any<string>()).Returns([]);
        }

        public string ProjectPath { get; }

        public string ThemesPath { get; }

        public static ThemeFixture Create(string workspaceRoot) => new(workspaceRoot);

        public string ResolveTarget(string targetName) => targetName == "ABSOLUTE"
            ? Path.GetDirectoryName(victim)!
            : targetName;

        public ThemeService CreateService()
        {
            var packageContext = Substitute.For<IPackageContext>();
            packageContext.Themes.Returns([]);

            return new ThemeService(
                registry,
                Substitute.For<ITemplateResolver>(),
                Substitute.For<IAssetResolver>(),
                packageContext,
                new PackageInstallService([], new PackageDeclarations(Substitute.For<IConfigService>(), Substitute.For<IGlobalConfigManager>(), NullLogger<PackageDeclarations>.Instance)),
                Substitute.For<IPackageIndexService>(),
                Substitute.For<IConfigService>(),
                Options.Create(new ProjectEnvironment { Path = ProjectPath }),
                CreateThemeConfig(),
                NullLogger<ThemeService>.Instance);
        }

        public ThemeExtractCommand CreateCommand() => new(
            Substitute.For<IThemeService>(),
            registry,
            Substitute.For<ITemplateResolver>(),
            Substitute.For<IAssetResolver>(),
            Options.Create(new ProjectEnvironment { Path = ProjectPath }),
            CreateThemeConfig(),
            FakeConsoleCapabilities.NonInteractive,
            NullLogger<ThemeExtractCommand>.Instance);

        public void AssertNothingDeleted()
        {
            Assert.IsTrue(File.Exists(projectJson), "project.json must not be deleted.");
            Assert.IsTrue(File.Exists(photo), "Source photo must not be deleted.");
            Assert.IsTrue(File.Exists(otherTheme), "Other themes must not be deleted.");
            Assert.IsTrue(File.Exists(victim), "Directories outside the project must not be deleted.");
        }

        private static IOptionsMonitor<ThemeConfig> CreateThemeConfig()
        {
            var monitor = Substitute.For<IOptionsMonitor<ThemeConfig>>();
            monitor.CurrentValue.Returns(new ThemeConfig { Name = SourceTheme });
            return monitor;
        }
    }
}
