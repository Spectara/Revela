using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

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
    public async Task GenerateAll_MissingSiteTitle_FailsWithFriendlyMessageNotScribanCrash()
    {
        // site.json exists but defines no title — the render step must fail early with a
        // clear message instead of the cryptic Scriban "site.title for a null object".
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "NoTitle" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "all"]);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("title", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null object", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unhandled exception", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
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
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = projectPath,
                EnvironmentName = "Testing",
            });

            builder.ConfigureRevela(args, new EmptyPackageSource());

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
