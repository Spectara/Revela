using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

using Spectre.Console;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// Verifies <c>generate all</c> fails gracefully now that validation is no longer a hidden
/// phase-0 step: friendly, actionable messages (not cryptic crashes), a non-zero exit code,
/// and the central "run revela check" hint.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class GenerateGracefulFailureTests
{
    [TestMethod]
    public async Task GenerateAll_MissingTheme_FailsWithFriendlyMessageAndCheckHint()
    {
        // No theme is installed (EmptyPackageSource) — the render step must fail with a
        // clear message instead of producing broken output.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "NoTheme" } })
            .WithSiteJson(new { title = "No Theme", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "all"]);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("not installed", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("revela check", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled exception", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task GenerateAll_MissingTheme_ScanStepFailsGracefullyWithoutStackTrace()
    {
        // Regression: the scan step reads image sizes from the resolved theme via the real
        // ImageSizesProvider. When no theme is installed (EmptyPackageSource) that provider
        // throws, and the generic catch in ContentService used to log the full exception +
        // stack trace above the friendly panel. The scan step must now pre-check the theme
        // and fail like the pages step — no raw stack trace, just an actionable message.
        // NOTE: unlike the other cases this must NOT stub IImageSizesProvider, otherwise the
        // real scan-step path (the one that leaked the trace) is never exercised.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "NoTheme" } })
            .WithSiteJson(new { title = "No Theme", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "all"], stubImageSizes: false);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("not installed", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("revela check", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled exception", output, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task GenerateAll_MissingSiteTitle_FailsWithFriendlyMessageNotScribanCrash()
    {
        // site.json exists but defines no title — the render step must fail early with a
        // clear message instead of the cryptic Scriban "site.title for a null object".
        // The theme is installed here so the scan step's theme pre-check passes and the
        // failure surfaces at the render title check (the behaviour under test).
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "NoTitle" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "all"], installTheme: true);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("title", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null object", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unhandled exception", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> RunCliAsync(
        string projectPath,
        string[] args,
        bool stubImageSizes = true,
        bool installTheme = false)
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
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = projectPath,
                EnvironmentName = "Testing",
            });

            builder.ConfigureRevela(args, new EmptyPackageSource());

            if (installTheme)
            {
                // A resolvable base theme so the scan step's theme pre-check passes.
                builder.Services.AddSingleton<ITheme>(new LuminaTheme());
            }

            if (stubImageSizes)
            {
                var imageSizes = Substitute.For<IImageSizesProvider>();
                imageSizes.GetSizes().Returns([320, 640]);
                imageSizes.GetResizeMode().Returns("longest");
                builder.Services.AddSingleton(imageSizes);
            }

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
