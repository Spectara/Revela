using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Statistics;
using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Statistics;

[TestClass]
[TestCategory("Unit")]
public sealed class StatisticsDataInvalidatorTests
{
    [TestMethod]
    public async Task InvalidateAsync_StatisticsFilesExist_DeletesStatisticsFilesOnly()
    {
        var projectPath = Path.Combine(
            Path.GetTempPath(),
            "revela-statistics-invalidation-tests",
            Guid.NewGuid().ToString());
        var cachePath = Path.Combine(projectPath, ProjectPaths.Cache, "statistics");
        Directory.CreateDirectory(cachePath);
        var statisticsPath = Path.Combine(cachePath, "statistics.json");
        var unrelatedPath = Path.Combine(cachePath, "calendar.json");
        await File.WriteAllTextAsync(statisticsPath, "{}");
        await File.WriteAllTextAsync(unrelatedPath, "{}");
        var invalidator = new StatisticsDataInvalidator(Options.Create(new ProjectEnvironment
        {
            Path = projectPath
        }));

        try
        {
            var result = await invalidator.InvalidateAsync();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(StatisticsArtifacts.Data, invalidator.Artifact);
            CollectionAssert.AreEqual(
                new[] { CoreArtifacts.Manifest },
                invalidator.DependsOn.ToArray());
            Assert.IsFalse(File.Exists(statisticsPath));
            Assert.IsTrue(File.Exists(unrelatedPath));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task InvalidateAsync_DirectoryLinkLeavesCache_PreservesExternalStatistics()
    {
        var projectPath = Path.Combine(
            Path.GetTempPath(),
            "revela-statistics-invalidation-tests",
            Guid.NewGuid().ToString());
        var cachePath = Path.Combine(projectPath, ProjectPaths.Cache);
        var externalDirectory = projectPath + "-external";
        var linkPath = Path.Combine(cachePath, "linked");
        Directory.CreateDirectory(cachePath);
        Directory.CreateDirectory(externalDirectory);
        var externalStatistics = Path.Combine(externalDirectory, "statistics.json");
        await File.WriteAllTextAsync(externalStatistics, "{}");
        DirectoryLinkTestHelper.Create(linkPath, externalDirectory);
        var invalidator = new StatisticsDataInvalidator(Options.Create(new ProjectEnvironment
        {
            Path = projectPath
        }));

        try
        {
            var result = await invalidator.InvalidateAsync();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(File.Exists(externalStatistics));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(linkPath);
            Directory.Delete(projectPath, recursive: true);
            Directory.Delete(externalDirectory, recursive: true);
        }
    }
}
