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

    public void Dispose()
    {
        if (Directory.Exists(projectPath))
        {
            Directory.Delete(projectPath, recursive: true);
        }
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
