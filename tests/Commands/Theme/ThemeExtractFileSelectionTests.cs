using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Features.Theme.Commands;
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
public sealed class ThemeExtractFileSelectionTests
{
    private const string ThemeName = "Lumina";
    private const string ImagesJson = /*lang=json,strict*/ """{ "sizes": [320] }""";
    private const string LocaleJson = /*lang=json,strict*/ """{ "photo.close": "Foto schließen" }""";
    private const string StatisticsLocaleJson = /*lang=json,strict*/ """{ "statistics.overview": "Übersicht" }""";

    [TestMethod]
    [DataRow("Configuration/images.json")]
    [DataRow("configuration/images.json")]
    [DataRow("Configuration/")]
    public async Task ExtractCommand_ConfigurationFile_WritesWhereGenerationReadsIt(string fileArgument)
    {
        using var project = TestProject.Create();
        var command = CreateCommand(project.RootPath).Create();

        var exitCode = await InvokeQuietAsync(command, ["--file", fileArgument, "--force"]);

        Assert.AreEqual(0, exitCode);
        var expected = Path.Combine(project.RootPath, ProjectPaths.Themes, ThemeName, "Configuration", "images.json");
        Assert.AreEqual(ImagesJson, await File.ReadAllTextAsync(expected));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.RootPath, "theme")), "Nothing reads a project-root theme/ folder");
        Assert.IsFalse(
            File.Exists(Path.Combine(project.RootPath, ProjectPaths.Themes, ThemeName, "manifest.json")),
            "A partial extraction must not copy the bundled manifest");
    }

    [TestMethod]
    [DataRow("Locales/de.json", "Locales/de.json")]
    [DataRow("locales/DE.json", "Locales/de.json")]
    [DataRow("Locales/Statistics/de.json", "Locales/Statistics/de.json")]
    public async Task ExtractCommand_LocaleFile_WritesWhereRenderingReadsIt(string fileArgument, string expectedRelativePath)
    {
        using var project = TestProject.Create();
        var command = CreateCommand(project.RootPath).Create();

        var exitCode = await InvokeQuietAsync(command, ["--file", fileArgument, "--force"]);

        Assert.AreEqual(0, exitCode);
        var expected = Path.Combine(project.RootPath, ProjectPaths.Themes, ThemeName, expectedRelativePath);
        var expectedContent = expectedRelativePath.Contains("Statistics", StringComparison.Ordinal) ? StatisticsLocaleJson : LocaleJson;
        Assert.AreEqual(expectedContent, await File.ReadAllTextAsync(expected));
    }

    [TestMethod]
    public async Task ExtractCommand_LocalesFolder_ExtractsThemeAndExtensionLocales()
    {
        using var project = TestProject.Create();
        var command = CreateCommand(project.RootPath).Create();

        var exitCode = await InvokeQuietAsync(command, ["--file", "Locales/", "--force"]);

        Assert.AreEqual(0, exitCode);
        var themeFolder = Path.Combine(project.RootPath, ProjectPaths.Themes, ThemeName);
        Assert.AreEqual(LocaleJson, await File.ReadAllTextAsync(Path.Combine(themeFolder, "Locales", "de.json")));
        Assert.AreEqual(StatisticsLocaleJson, await File.ReadAllTextAsync(Path.Combine(themeFolder, "Locales", "Statistics", "de.json")));
    }

    private static ThemeExtractCommand CreateCommand(string projectPath)
    {
        var theme = Substitute.For<ITheme>();
        theme.Metadata.Returns(new PackageMetadata
        {
            Id = "Spectara.Revela.Themes.Lumina",
            Name = ThemeName,
            Version = "1.0.0",
            Description = "Test theme",
            Author = "Test"
        });
        theme.GetAllFiles().Returns(["manifest.json", "Configuration/images.json", "Layout.revela", "Locales/de.json"]);
        theme.GetFile("Configuration/images.json").Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ImagesJson)));
        theme.GetFile("Locales/de.json").Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(LocaleJson)));
        theme.GetFile("manifest.json").Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(/*lang=json,strict*/ """{ "name": "Lumina" }""")));

        var extension = Substitute.For<ITheme>();
        extension.Metadata.Returns(new PackageMetadata
        {
            Id = "Spectara.Revela.Themes.Lumina.Statistics",
            Name = "Lumina Statistics",
            Version = "1.0.0",
            Description = "Test extension",
            Author = "Test"
        });
        extension.Prefix.Returns("statistics");
        extension.GetAllFiles().Returns(["manifest.json", "Locales/de.json"]);
        extension.GetFile("Locales/de.json").Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(StatisticsLocaleJson)));

        var registry = Substitute.For<IThemeRegistry>();
        registry.Resolve(ThemeName, projectPath).Returns(theme);
        registry.GetExtensions(Arg.Any<string>()).Returns([extension]);

        var themeConfig = Substitute.For<IOptionsMonitor<ThemeConfig>>();
        themeConfig.CurrentValue.Returns(new ThemeConfig { Name = ThemeName });

        return new ThemeExtractCommand(
            Substitute.For<IThemeService>(),
            registry,
            Substitute.For<ITemplateResolver>(),
            Substitute.For<IAssetResolver>(),
            Options.Create(new ProjectEnvironment { Path = projectPath }),
            themeConfig,
            NullLogger<ThemeExtractCommand>.Instance);
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
}
