using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Statistics.Commands;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

using Spectre.Console;

namespace Spectara.Revela.Tests.Plugins.Statistics;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CleanStatisticsCommandTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_DirectoryLinkInCache_PreservesExternalStatistics(bool useCli)
    {
        using var project = TestProject.CreateMinimal();
        var cachePath = Path.Combine(project.RootPath, ProjectPaths.GetOwnerDirectory("statistics"));
        var owned = Path.Combine(cachePath, "statistics", "statistics.json");
        Directory.CreateDirectory(Path.GetDirectoryName(owned)!);
        await File.WriteAllTextAsync(owned, "{}");
        var external = project.RootPath + "-external";
        Directory.CreateDirectory(external);
        var externalStatistics = Path.Combine(external, "statistics.json");
        await File.WriteAllTextAsync(externalStatistics, "{}");
        var link = Path.Combine(cachePath, "linked");
        DirectoryLinkTestHelper.Create(link, external);
        var command = new CleanStatisticsCommand(new TestArtifactLifecycle(
            new StatisticsDataInvalidator(Options.Create(new ProjectEnvironment { Path = project.RootPath }))));

        try
        {
            var success = useCli
                ? await CaptureAsync(() => command.ExecuteAsync(CancellationToken.None)) == 0
                : (await ((IPipelineStep)command).ExecuteAsync()).Success;

            Assert.IsTrue(success);
            Assert.IsFalse(File.Exists(owned));
            Assert.IsTrue(File.Exists(externalStatistics));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(link);
            Directory.Delete(external, recursive: true);
        }
    }

    private static async Task<int> CaptureAsync(Func<Task<int>> execute)
    {
        var originalConsole = AnsiConsole.Console;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });

        try
        {
            return await execute();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
