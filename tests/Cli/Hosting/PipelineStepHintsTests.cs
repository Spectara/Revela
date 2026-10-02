using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Plugins.Serve;
using Spectara.Revela.Plugins.Statistics;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

using Spectre.Console;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// Verifies that per-step "Next steps" hints are only shown for standalone step runs:
/// inside <c>generate all</c> the steps stay quiet and the pipeline prints one final hint.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class PipelineStepHintsTests
{
    private const string NextStepsHeading = "Next steps:";

    [TestMethod]
    public async Task GenerateAll_Success_OmitsPerStepNextStepsAndShowsSingleFinalHint()
    {
        using var project = CreateProjectWithStatisticsPage();

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "all"]);

        Assert.AreEqual(0, exitCode, output);
        Assert.Contains("Statistics generated!", output, StringComparison.Ordinal);
        Assert.Contains("Page rendering complete!", output, StringComparison.Ordinal);
        Assert.Contains("Image processing complete!", output, StringComparison.Ordinal);
        Assert.Contains("Pipeline completed", output, StringComparison.Ordinal);
        Assert.DoesNotContain(NextStepsHeading, output, StringComparison.Ordinal);
        Assert.DoesNotContain("revela generate pages", output, StringComparison.Ordinal);
        Assert.DoesNotContain("revela generate all", output, StringComparison.Ordinal);

        var finalHint = output[output.IndexOf("Pipeline completed", StringComparison.Ordinal)..];
        Assert.Contains("revela serve", finalHint, StringComparison.Ordinal);
        Assert.Contains("output", finalHint, StringComparison.Ordinal);
        Assert.AreEqual(1, CountOccurrences(output, "revela serve"));
    }

    [TestMethod]
    public async Task StandaloneSteps_Success_KeepNextStepsHints()
    {
        using var project = CreateProjectWithStatisticsPage();

        var (scanExitCode, scanOutput) = await RunCliAsync(project.RootPath, ["generate", "scan"]);
        var (statsExitCode, statsOutput) = await RunCliAsync(project.RootPath, ["generate", "statistics"]);

        Assert.AreEqual(0, scanExitCode, scanOutput);
        Assert.Contains(NextStepsHeading, scanOutput, StringComparison.Ordinal);
        Assert.Contains("revela generate pages", scanOutput, StringComparison.Ordinal);

        Assert.AreEqual(0, statsExitCode, statsOutput);
        Assert.Contains("Statistics generated!", statsOutput, StringComparison.Ordinal);
        Assert.Contains(NextStepsHeading, statsOutput, StringComparison.Ordinal);
        Assert.Contains("revela generate pages", statsOutput, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task GenerateAll_MarkupProjectNameUnderGermanCulture_EscapesNameAndFormatsInvariantDurations()
    {
        using var project = CreateProjectWithStatisticsPage("[Hints]");
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            var (exitCode, output) = await RunCliAsync(project.RootPath, ["generate", "all"]);

            Assert.AreEqual(0, exitCode, output);
            Assert.Contains("[Hints]", output, StringComparison.Ordinal);
            Assert.MatchesRegex(@"Duration:\s+\d+\.\d{2}s", output);
            Assert.DoesNotMatchRegex(@"Duration:\s+\d+,\d{2}s", output);
            Assert.MatchesRegex(@"Pipeline completed in \d+\.\d{2}s", output);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static TestProject CreateProjectWithStatisticsPage(string projectName = "Hints")
    {
        var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = projectName },
                theme = new { name = "Lumina" },
                generate = new { images = new { jpg = 90 } },
            })
            .WithSiteJson(new { title = "Hints", author = "Test" })
            .AddGallery("Landscapes", g => g.AddRealImage("sunset.jpg", 640, 480)));

        var statsPath = Path.Combine(project.SourcePath, "Stats");
        Directory.CreateDirectory(statsPath);
        File.WriteAllText(
            Path.Combine(statsPath, "_index.revela"),
            "+++\ntitle = \"Stats\"\ndata.statistics = \"statistics.json\"\n+++\n");

        return project;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static async Task<(int ExitCode, string Output)> RunCliAsync(string projectPath, string[] args)
    {
        var originalConsole = AnsiConsole.Console;
        using var writer = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });
        AnsiConsole.Console.Profile.Width = 240;

        try
        {
            var builder = HostBootstrap.CreateBuilder(args, new StatisticsPackageSource(), projectPath);
            builder.Services.AddSingleton<ITheme>(new LuminaTheme());

            var imageSizes = Substitute.For<IImageSizesProvider>();
            imageSizes.GetSizes().Returns([320]);
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

    private sealed class StatisticsPackageSource : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() =>
        [
            new LoadedPluginInfo(new StatisticsPlugin(), PackageSource.Bundled),
            new LoadedPluginInfo(new ServePlugin(), PackageSource.Bundled),
        ];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }
}
