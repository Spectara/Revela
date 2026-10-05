using System.Globalization;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

using Spectara.Revela.Plugins.Statistics.Commands;
using Spectara.Revela.Plugins.Statistics.Configuration;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Models.Manifest;

using Spectre.Console;

namespace Spectara.Revela.Tests.Plugins.Statistics;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class StatsCommandTests : IDisposable
{
    private readonly string projectPath = Path.Combine(
        Path.GetTempPath(),
        "revela-statistics-command-tests",
        Guid.NewGuid().ToString());

    [TestMethod]
    public async Task ExecuteAsync_NoImages_RemovesOwnedArtifacts()
    {
        var statisticsPath = await CreateStatisticsArtifactAsync();
        var command = CreateCommand(new SuccessfulArtifactLifecycle());

        var exitCode = await command.ExecuteAsync();

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(File.Exists(statisticsPath));
    }

    [TestMethod]
    public async Task PipelineExecuteAsync_NoUsableManifest_FailsAskingForScanAndPreservesArtifacts()
    {
        var statisticsPath = await CreateStatisticsArtifactAsync();
        var command = CreateCommand(new SuccessfulArtifactLifecycle(), manifest: null);

        var result = await ((IPipelineStep)command).ExecuteAsync();

        Assert.IsFalse(result.Success);
        Assert.Contains("run scan first", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(statisticsPath));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoUsableManifest_ReportsMissingScanAndPreservesArtifacts()
    {
        var statisticsPath = await CreateStatisticsArtifactAsync();
        var command = CreateCommand(new SuccessfulArtifactLifecycle(), manifest: null);

        var (exitCode, output) = await CaptureAsync(() => command.ExecuteAsync());

        Assert.AreEqual(1, exitCode);
        Assert.Contains("generate scan", output, StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(statisticsPath));
    }

    [TestMethod]
    public async Task ExecuteAsync_DependentInvalidationFails_PreservesOwnedArtifacts()
    {
        var statisticsPath = await CreateStatisticsArtifactAsync();
        var command = CreateCommand(new FailingArtifactLifecycle());

        var exitCode = await command.ExecuteAsync();

        Assert.AreEqual(1, exitCode);
        Assert.IsTrue(File.Exists(statisticsPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_CustomDataFileName_WritesFrontMatterFileName(bool useCli)
    {
        var defaultPath = await CreateStatisticsArtifactAsync();
        var command = CreateCommand(new SuccessfulArtifactLifecycle(), ScanWithStatisticsPage("camera-stats.json"));

        var success = await ExecuteAsync(command, useCli);

        Assert.IsTrue(success);
        var customPath = Path.Combine(projectPath, ProjectPaths.GetOwnerDirectory("statistics"), "statistics", "camera-stats.json");
        Assert.IsTrue(File.Exists(customPath));
        Assert.IsFalse(File.Exists(defaultPath), "Data under the default name is stale once the page names another file.");
    }

    [TestMethod]
    public async Task PipelineExecuteAsync_StatisticsTemplateWithoutData_WritesDefaultFileName()
    {
        var scan = new ManifestSnapshot
        {
            Root = new ManifestEntry { Text = "Stats", Path = "statistics", Template = "statistics/overview" },
            Images = OneImage,
        };
        var command = CreateCommand(new SuccessfulArtifactLifecycle(), scan);

        var result = await ((IPipelineStep)command).ExecuteAsync();

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.IsTrue(File.Exists(Path.Combine(projectPath, ProjectPaths.GetOwnerDirectory("statistics"), "statistics", "statistics.json")));
    }

    [TestMethod]
    [DataRow("../escape.json", false)]
    [DataRow("nested/statistics.json", false)]
    [DataRow("statistics.txt", true)]
    [DataRow("$images", true)]
    public async Task Execute_InvalidDataFileName_FailsWithHintAndPreservesArtifacts(string dataFileName, bool useCli)
    {
        var statisticsPath = await CreateStatisticsArtifactAsync();
        var command = CreateCommand(new SuccessfulArtifactLifecycle(), ScanWithStatisticsPage(dataFileName));

        string message;
        if (useCli)
        {
            var (exitCode, output) = await CaptureAsync(() => command.ExecuteAsync());
            Assert.AreEqual(1, exitCode);
            message = output;
        }
        else
        {
            var result = await ((IPipelineStep)command).ExecuteAsync();
            Assert.IsFalse(result.Success);
            message = result.ErrorMessage!;
        }

        Assert.Contains("statistics.json", message, StringComparison.Ordinal);
        Assert.Contains("data.statistics", message, StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(statisticsPath));
        Assert.IsFalse(File.Exists(Path.Combine(projectPath, ProjectPaths.GetOwnerDirectory("statistics"), "escape.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(projectPath))
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    private static readonly Dictionary<string, ImageContent> OneImage = new()
    {
        ["a.jpg"] = new ImageContent { Filename = "a.jpg", Width = 10, Height = 10, Sizes = [] },
    };

    private static ManifestSnapshot ScanWithStatisticsPage(string dataFileName) => new()
    {
        Root = new ManifestEntry
        {
            Text = "Stats",
            Path = "statistics",
            DataSources = new Dictionary<string, string> { ["statistics"] = dataFileName },
        },
        Images = OneImage,
    };

    private static async Task<bool> ExecuteAsync(StatsCommand command, bool useCli)
    {
        if (useCli)
        {
            var (exitCode, _) = await CaptureAsync(() => command.ExecuteAsync());
            return exitCode == 0;
        }

        var result = await ((IPipelineStep)command).ExecuteAsync();
        return result.Success;
    }

    private async Task<string> CreateStatisticsArtifactAsync()
    {
        var cachePath = Path.Combine(projectPath, ProjectPaths.GetOwnerDirectory("statistics"));
        var statisticsDirectory = Path.Combine(cachePath, "statistics");
        Directory.CreateDirectory(statisticsDirectory);
        await File.WriteAllTextAsync(Path.Combine(cachePath, "manifest.json"), "{}");
        var statisticsPath = Path.Combine(statisticsDirectory, "statistics.json");
        await File.WriteAllTextAsync(statisticsPath, "{}");
        return statisticsPath;
    }

    private static readonly ManifestSnapshot EmptyScan = new()
    {
        Root = new ManifestEntry { Text = "Home", Path = "" },
        Images = new Dictionary<string, ImageContent>()
    };

    private StatsCommand CreateCommand(IArtifactLifecycle artifactLifecycle) =>
        CreateCommand(artifactLifecycle, EmptyScan);

    private StatsCommand CreateCommand(IArtifactLifecycle artifactLifecycle, ManifestSnapshot? manifest)
    {
        var manifestReader = Substitute.For<IManifestReader>();
        manifestReader.TryLoadAsync(Arg.Any<CancellationToken>()).Returns(manifest);
        var config = Substitute.For<IOptionsMonitor<StatisticsPluginConfig>>();
        config.CurrentValue.Returns(new StatisticsPluginConfig());
        var environment = Options.Create(new ProjectEnvironment { Path = projectPath });
        var aggregator = new StatisticsAggregator(
            config,
            TimeProvider.System,
            NullLogger<StatisticsAggregator>.Instance);
        return new StatsCommand(
            NullLogger<StatsCommand>.Instance,
            manifestReader,
            environment,
            aggregator,
            artifactLifecycle,
            new StatisticsDataInvalidator(environment));
    }

    private static async Task<(int ExitCode, string Output)> CaptureAsync(Func<Task<int>> execute)
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
        AnsiConsole.Console.Profile.Width = 240;
        try
        {
            var exitCode = await execute();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed class SuccessfulArtifactLifecycle : IArtifactLifecycle
    {
        public ValueTask<OperationResult> InvalidateAsync(ArtifactId artifact, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<OperationResult> InvalidateAllAsync(IReadOnlyCollection<ArtifactKind> kinds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<OperationResult> PrepareToReplaceAsync(
            ArtifactId artifact,
            CancellationToken cancellationToken = default)
        {
            _ = artifact;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<OperationResult>(OperationResult.Ok());
        }
    }

    private sealed class FailingArtifactLifecycle : IArtifactLifecycle
    {
        public ValueTask<OperationResult> InvalidateAsync(ArtifactId artifact, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<OperationResult> InvalidateAllAsync(IReadOnlyCollection<ArtifactKind> kinds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<OperationResult> PrepareToReplaceAsync(
            ArtifactId artifact,
            CancellationToken cancellationToken = default)
        {
            _ = artifact;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<OperationResult>(
                OperationResult.Fail("dependent cleanup failed"));
        }
    }
}
