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

namespace Spectara.Revela.Tests.Plugins.Statistics;

[TestClass]
[TestCategory("Unit")]
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
        var cachePath = Path.Combine(projectPath, ProjectPaths.Cache);
        var statisticsDirectory = Path.Combine(cachePath, "statistics");
        Directory.CreateDirectory(statisticsDirectory);
        await File.WriteAllTextAsync(Path.Combine(cachePath, "manifest.json"), "{}");
        var statisticsPath = Path.Combine(statisticsDirectory, "statistics.json");
        await File.WriteAllTextAsync(statisticsPath, "{}");
        return statisticsPath;
    }

    private StatsCommand CreateCommand(IArtifactLifecycle artifactLifecycle)
    {
        var manifestRepository = Substitute.For<IManifestRepository>();
        manifestRepository.Images.Returns(new Dictionary<string, ImageContent>());
        var config = Substitute.For<IOptionsMonitor<StatisticsPluginConfig>>();
        config.CurrentValue.Returns(new StatisticsPluginConfig());
        var environment = Options.Create(new ProjectEnvironment { Path = projectPath });
        var aggregator = new StatisticsAggregator(
            manifestRepository,
            config,
            TimeProvider.System,
            NullLogger<StatisticsAggregator>.Instance);
        return new StatsCommand(
            NullLogger<StatsCommand>.Instance,
            manifestRepository,
            environment,
            aggregator,
            artifactLifecycle,
            new StatisticsDataInvalidator(environment));
    }

    private sealed class SuccessfulArtifactLifecycle : IArtifactLifecycle
    {
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
