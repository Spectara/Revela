using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;
using Spectre.Console;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// Verifies the CLI boundary turns a configuration validation failure into a
/// clean, styled panel with exit code 2 instead of a raw unhandled exception
/// (issue #75 — a stray <c>language</c> left in <c>project.json</c>).
/// </summary>
/// <remarks>
/// These tests capture the shared <see cref="AnsiConsole.Console"/>, so they must
/// not run in parallel with each other.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class ConfigErrorExitCodeTests
{
    [TestMethod]
    public async Task RunRevelaAsync_ProjectJsonWithStrayLanguage_ExitsWithCode2AndFriendlyMessage()
    {
        // Arrange: 'language' moved to site.json (#75) — a stray value in
        // project.json must surface as a friendly error, not a crash.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "My Portfolio", language = "de" }
            }));

        // Act
        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "scan"]);

        // Assert: config-error exit code, plain-language panel, no leaked jargon or stack trace.
        Assert.AreEqual(2, exitCode);
        Assert.Contains("Configuration problem", output, StringComparison.Ordinal);
        Assert.Contains("site.json", output, StringComparison.Ordinal);
        Assert.DoesNotContain("OptionsValidationException", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunRevelaAsync_GenerateAllPipeline_WithStrayLanguage_ExitsWithCode2AndNoStackTrace()
    {
        // Arrange: `generate all` no longer runs a hidden check phase. The stray
        // project.language (#75) is surfaced lazily the first time a pipeline step touches
        // the invalid ProjectConfig, rendered as the friendly configuration-problem panel —
        // still exit 2, still no leaked stack trace.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "My Portfolio", language = "de" }
            }));

        // Act
        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "all"]);

        // Assert: surfaced by the configuration-problem panel, exit 2, no raw exception.
        Assert.AreEqual(2, exitCode);
        Assert.Contains("Configuration problem", output, StringComparison.Ordinal);
        Assert.Contains("language", output, StringComparison.Ordinal);
        Assert.Contains("site.json", output, StringComparison.Ordinal);
        Assert.DoesNotContain("OptionsValidationException", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled exception", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunRevelaAsync_ValidProjectJson_DoesNotReportConfigProblem()
    {
        // Arrange: a valid project.json (language lives in site.json) runs normally.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "My Portfolio" }
            }));

        // Act
        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "scan"]);

        // Assert: never the config-error path.
        Assert.AreNotEqual(2, exitCode);
        Assert.DoesNotContain("Configuration problem", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunRevelaAsync_ProjectJsonWithAbsoluteUrlBasePath_ExitsWithCode2AndPointsToBaseUrl()
    {
        // Arrange: basePath is a subdirectory prefix, not a host (#76). An absolute
        // URL must fail with a friendly hint pointing at baseUrl.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "My Portfolio", basePath = "https://cdn.example.com" }
            }));

        // Act
        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "scan"]);

        // Assert
        Assert.AreEqual(2, exitCode);
        Assert.Contains("Configuration problem", output, StringComparison.Ordinal);
        Assert.Contains("baseUrl", output, StringComparison.Ordinal);
        Assert.DoesNotContain("OptionsValidationException", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunRevelaAsync_ProjectJsonWithSubdirectoryBasePath_DoesNotReportConfigProblem()
    {
        // Arrange: a valid subdirectory basePath (#76) must pass validation.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "My Portfolio", basePath = "/photos/" }
            }));

        // Act
        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "scan"]);

        // Assert
        Assert.AreNotEqual(2, exitCode);
        Assert.DoesNotContain("Configuration problem", output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> RunCliAsync(string projectPath, string[] args)
    {
        var originalConsole = AnsiConsole.Console;
        var writer = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });

        try
        {
            var builder = HostBootstrap.CreateBuilder(args, new EmptyPackageSource(), projectPath);

            // A resolvable base theme so the scan step's theme pre-check passes and the
            // pipeline reaches the ProjectConfig validator — the behaviour under test.
            builder.Services.AddSingleton<ITheme>(new LuminaTheme());

            // Stub image sizes so `generate scan` completes without a real theme —
            // the point under test is the ProjectConfig validator firing during the
            // command, not image processing.
            var imageSizes = Substitute.For<IImageSizesProvider>();
            imageSizes.GetSizes().Returns([320, 640]);
            imageSizes.GetResizeMode().Returns("longest");
            builder.Services.AddSingleton(imageSizes);

            using var host = builder.Build();
            var exitCode = await host.RunRevelaAsync(args);

            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed class EmptyPackageSource : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() => [];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }
}
