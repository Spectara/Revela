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
/// Verifies the first-class <c>check</c> command group: its placement under Setup, the
/// host-wrapped <c>check &lt;name&gt;</c> sub-commands, the single bespoke <c>check all</c>,
/// and end-to-end exit codes.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CheckCommandTests
{
    [TestMethod]
    public void CommandTree_Check_IsUnderSetupWithWrappedUnitsAndSingleAll()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Tree" } }));

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = project.RootPath,
            EnvironmentName = "Testing",
        });
        builder.ConfigureRevela([], new EmptyPackageSource());
        using var host = builder.Build();

        var rootCommand = host.UseRevelaCommands();
        var orderRegistry = host.Services.GetRequiredService<CommandOrderRegistry>();

        var check = rootCommand.Subcommands.Single(c => string.Equals(c.Name, "check", StringComparison.Ordinal));

        // `check` lives in Setup, not Build.
        Assert.AreEqual(CommandGroups.Setup, orderRegistry.GetGroup(check));

        // Every host check is wrapped as a `check <name>` sub-command.
        var names = check.Subcommands.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(
            names.IsSupersetOf(["all", "config", "structure", "theme", "content", "slugs"]),
            $"Missing expected check sub-commands. Found: {string.Join(", ", names)}");

        // Exactly one `all` — the bespoke collect-all, not an auto-generated duplicate.
        Assert.HasCount(1, check.Subcommands.Where(c => string.Equals(c.Name, "all", StringComparison.Ordinal)).ToList());

        // The units carry the `●` "included in all" marker; `all` does not.
        var configUnit = check.Subcommands.Single(c => string.Equals(c.Name, "config", StringComparison.Ordinal));
        var allCommand = check.Subcommands.Single(c => string.Equals(c.Name, "all", StringComparison.Ordinal));
        Assert.IsTrue(orderRegistry.IsPipelineStep(configUnit), "check config should be an included-in-all step.");
        Assert.IsFalse(orderRegistry.IsPipelineStep(allCommand), "check all is the aggregator, not a step.");

        // There is no longer a hidden `generate check` phase-0 step.
        var generate = rootCommand.Subcommands.Single(c => string.Equals(c.Name, "generate", StringComparison.Ordinal));
        Assert.IsFalse(
            generate.Subcommands.Any(c => string.Equals(c.Name, "check", StringComparison.Ordinal)),
            "generate must not expose a hidden 'check' step.");
    }

    [TestMethod]
    public async Task CheckAll_StrayProjectLanguage_ExitsTwoWithUnifiedReport()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Bad", language = "de" } }));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["check", "all"]);

        Assert.AreEqual(2, exitCode);
        Assert.Contains("Check found problems", output, StringComparison.Ordinal);
        Assert.Contains("language", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task BareCheck_StrayProjectLanguage_BehavesLikeCheckAll()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Bad", language = "de" } }));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["check"]);

        Assert.AreEqual(2, exitCode);
        Assert.Contains("Check found problems", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CheckConfig_StrayProjectLanguage_ExitsTwo()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Bad", language = "de" } }));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["check", "config"]);

        Assert.AreEqual(2, exitCode);
        Assert.Contains("language", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CheckSlugs_CollidingGalleries_ExitsTwo()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Collision" } })
            .WithSiteJson(new { title = "Collision", author = "Test" })
            .AddGallery("01 Events", g => g.AddImage("a.jpg"))
            .AddGallery("Events", g => g.AddImage("b.jpg")));

        var (exitCode, output) = await RunCliAsync(project.RootPath, ["check", "slugs"]);

        Assert.AreEqual(2, exitCode);
        Assert.Contains("Slug collision", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CheckConfig_ValidProject_ExitsZero()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Good", baseUrl = "https://example.com" } })
            .WithSiteJson(new { title = "Good", author = "Test" }));

        var (exitCode, _) = await RunCliAsync(project.RootPath, ["check", "config"]);

        Assert.AreEqual(0, exitCode);
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
