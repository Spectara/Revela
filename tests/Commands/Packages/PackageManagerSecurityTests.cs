using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Tests.Shared.Http;

using NuGetPackageSource = NuGet.Configuration.PackageSource;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class PackageManagerSecurityTests
{
    private const string FixtureId = "Spectara.Revela.Plugins.Fixture";
    private const string InsecureFeed = "http://feed.invalid/v3/index.json";

    private string root = null!;

    [TestInitialize]
    public void Initialize() => root = Directory.CreateTempSubdirectory("revela-pm-").FullName;

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("../evil")]
    [DataRow("Spectara.Revela..Evil")]
    [DataRow("Spectara Revela")]
    public async Task InstallAsync_InvalidPackageId_RejectedBeforeQueryingSources(string packageId)
    {
        using var httpClient = new HttpClient();
        var (manager, logger, sourceManager) = CreateManager(httpClient);

        var installed = await manager.InstallAsync(packageId);

        Assert.IsNull(installed);
        _ = await sourceManager.DidNotReceive().LoadSourcesAsync(Arg.Any<CancellationToken>());
        Assert.IsTrue(logger.Entries.Any(e => e.Message.Contains("Invalid package ID", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task InstallAsync_PlainHttpPackageUrl_RejectedWithoutDownload()
    {
        using var handler = new MockHttpMessageHandler();
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        handler.AddPatternResponse(_ => true, response);
        using var httpClient = new HttpClient(handler);
        var (manager, logger, _) = CreateManager(httpClient);

        var installed = await manager.InstallAsync("http://packages.test/Spectara.Revela.Plugins.Fixture.1.0.0.nupkg");

        Assert.IsNull(installed);
        Assert.IsEmpty(handler.RecordedRequests);
        Assert.IsTrue(logger.Entries.Any(e => e.Message.Contains("insecure package source", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(InsecureFeed)]
    [DataRow("corp")]
    public async Task InstallAsync_PlainHttpExplicitSource_RejectedBeforeContactingFeed(string source)
    {
        using var httpClient = new HttpClient();
        var (manager, logger, sourceManager) = CreateManager(httpClient);
        sourceManager.LoadSourcesAsync(Arg.Any<CancellationToken>())
            .Returns([new NuGetSource { Name = "corp", Url = InsecureFeed }]);

        var installed = await manager.InstallAsync(FixtureId, source: source);

        Assert.IsNull(installed);
        Assert.IsTrue(logger.Entries.Any(e => e.Message.Contains("insecure package source", StringComparison.Ordinal)));
        Assert.IsFalse(logger.Entries.Any(e => e.Exception is not null));
    }

    [TestMethod]
    public async Task InstallAsync_PlainHttpConfiguredSource_IsSkipped()
    {
        using var httpClient = new HttpClient();
        var (manager, logger, sourceManager) = CreateManager(httpClient);
        sourceManager.LoadSourcesAsync(Arg.Any<CancellationToken>())
            .Returns([new NuGetSource { Name = "corp", Url = InsecureFeed }]);

        var installed = await manager.InstallAsync(FixtureId);

        Assert.IsNull(installed);
        Assert.IsTrue(logger.Entries.Any(e => e.Message.Contains("insecure package source", StringComparison.Ordinal)));
        Assert.IsFalse(logger.Entries.Any(e => e.Message.Contains("'corp' failed", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("1.0.0", null, "1.0.0")]
    [DataRow("dev-build", null, "1.0.0")]
    [DataRow("0.0.1-beta.21", null, "1.1.0-beta.1")]
    [DataRow("1.0.0", "1.1.0-beta.1", "1.1.0-beta.1")]
    [DataRow("1.0.0", "latest", "1.0.0")]
    [DataRow("0.0.1-beta.21", "Latest", "1.1.0-beta.1")]
    [DataRow("1.0.0", "", "1.0.0")]
    public async Task ExtractFromNuGetAsync_VersionSelection_HonorsHostPrereleaseState(string hostVersion, string? requested, string expected)
    {
        var feed = Path.Combine(root, "feed");
        _ = TestPackageFactory.CreatePackage(feed, FixtureId, "1.0.0");
        _ = TestPackageFactory.CreatePackage(feed, FixtureId, "1.1.0-beta.1");
        var buildInfo = Substitute.For<IBuildInfo>();
        buildInfo.Version.Returns(hostVersion);
        using var httpClient = new HttpClient();
        var (manager, _, _) = CreateManager(httpClient, buildInfo);
        var repository = Repository.Factory.GetCoreV3(new NuGetPackageSource(feed));

        var package = await manager.ExtractFromNuGetAsync(FixtureId, requested, repository, Path.Combine(root, "plugins"), CancellationToken.None);

        Assert.IsNotNull(package);
        Assert.AreEqual(expected, package.Version);
        Assert.AreEqual(FixtureId, package.Id);
        Assert.AreEqual("RevelaPlugin", package.PackageTypes.Single());
    }

    [TestMethod]
    [DataRow("not-a-version")]
    [DataRow(">=1.0.0")]
    public async Task ExtractFromNuGetAsync_UnparsableVersion_ReturnsNullAndLogsFailure(string requested)
    {
        var feed = Path.Combine(root, "feed");
        _ = TestPackageFactory.CreatePackage(feed, FixtureId, "1.0.0");
        var targetDir = Path.Combine(root, "plugins");
        using var httpClient = new HttpClient();
        var (manager, logger, _) = CreateManager(httpClient);
        var repository = Repository.Factory.GetCoreV3(new NuGetPackageSource(feed));

        var identity = await manager.ExtractFromNuGetAsync(FixtureId, requested, repository, targetDir, CancellationToken.None);

        Assert.IsNull(identity);
        Assert.IsFalse(Directory.Exists(targetDir));
        Assert.IsTrue(logger.Entries.Any(e =>
            e.Level == LogLevel.Error && e.Exception is null &&
            e.Message.Contains("Invalid version", StringComparison.Ordinal) &&
            e.Message.Contains(requested, StringComparison.Ordinal)));
    }

    private static (PackageManager Manager, RecordingLogger<PackageManager> Logger, INuGetSourceManager SourceManager) CreateManager(
        HttpClient httpClient,
        IBuildInfo? buildInfo = null)
    {
        var logger = new RecordingLogger<PackageManager>();
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.LoadSourcesAsync(Arg.Any<CancellationToken>()).Returns([]);
        if (buildInfo is null)
        {
            buildInfo = Substitute.For<IBuildInfo>();
            buildInfo.Version.Returns("0.0.1-beta.21");
        }

        var manager = new PackageManager(
            httpClient,
            new NupkgExtractor(NullLogger<NupkgExtractor>.Instance),
            logger,
            sourceManager,
            buildInfo);
        return (manager, logger, sourceManager);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
