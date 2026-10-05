using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Commands.Config.Feed;
using Spectara.Revela.Features.Packages.Commands.Packages;
using Spectara.Revela.Features.Packages.Services;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Tests.Shared.Http;

namespace Spectara.Revela.Tests.Commands.Packages;

/// <summary>
/// Feed URLs may carry credentials (user:token@host) or signed query strings. Logs and console
/// echoes must only show scheme, host, port and path.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class FeedUrlRedactionTests
{
    private const string Secret = "SYNTHETIC_FEED_SECRET";
    private const string FixtureId = "Spectara.Revela.Plugins.Fixture";

    [TestMethod]
    [DataRow("https://user:" + Secret + "@feed.example/v3/index.json")]
    [DataRow("https://" + Secret + "@feed.example/v3/index.json")]
    [DataRow("http://user:" + Secret + "@localhost:5555/v3/index.json")]
    public async Task FeedAdd_UrlWithUserInfo_IsRejectedWithoutSavingOrEchoingCredentials(string location)
    {
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var logger = new RecordingLogger<AddCommand>();
        var command = new AddCommand(logger, globalConfig).Create();

        var (exitCode, output) = await ConsoleCapture.InvokeAsync(command, "private", location);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("credentials", output);
        Assert.DoesNotContain(Secret, output, StringComparison.Ordinal);
        Assert.IsFalse(logger.Messages.Any(message => message.Contains(Secret, StringComparison.Ordinal)));
        Assert.IsEmpty(globalConfig.ReceivedCalls());
    }

    [TestMethod]
    public async Task FeedAdd_UrlWithQueryToken_SavesFeedButRedactsLogAndOutput()
    {
        const string location = "https://feed.example/v3/index.json?sig=" + Secret;
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var logger = new RecordingLogger<AddCommand>();
        var command = new AddCommand(logger, globalConfig).Create();

        var (exitCode, output) = await ConsoleCapture.InvokeAsync(command, "private", location);

        Assert.AreEqual(0, exitCode);
        await globalConfig.Received(1).AddFeedAsync("private", location, Arg.Any<CancellationToken>());
        Assert.Contains("https://feed.example/v3/index.json", output);
        Assert.DoesNotContain(Secret, output, StringComparison.Ordinal);
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("https://feed.example/v3/index.json", StringComparison.Ordinal)));
        Assert.IsFalse(logger.Messages.Any(message => message.Contains(Secret, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task FeedAdd_InsecureUrlWithQueryToken_RejectsWithRedactedUrl()
    {
        const string location = "http://feed.example/v3/index.json?sig=" + Secret;
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new AddCommand(NullLogger<AddCommand>.Instance, globalConfig).Create();

        var (exitCode, output) = await ConsoleCapture.InvokeAsync(command, "private", location);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("http://feed.example/v3/index.json", output);
        Assert.DoesNotContain(Secret, output, StringComparison.Ordinal);
        Assert.IsEmpty(globalConfig.ReceivedCalls());
    }

    [TestMethod]
    public async Task PackageManager_SourceUrlsWithCredentials_AreRedactedInLogs()
    {
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var logger = new RecordingLogger<PackageManager>();
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.LoadSourcesAsync(Arg.Any<CancellationToken>()).Returns(
            [new NuGetSource { Name = "corp", Url = "http://user:" + Secret + "@feed.invalid/v3/index.json?sig=" + Secret }]);
        var buildInfo = Substitute.For<IBuildInfo>();
        buildInfo.Version.Returns("0.0.1-beta.21");
        var manager = new PackageManager(httpClient, new NupkgExtractor(NullLogger<NupkgExtractor>.Instance), logger, sourceManager, buildInfo);

        // Configured insecure source is skipped, named source resolves and is rejected,
        // explicit source URL is rejected, plain-http package URL is rejected.
        await manager.InstallAsync(FixtureId, PackageIds.PluginPackageType);
        await manager.InstallAsync(FixtureId, PackageIds.PluginPackageType, source: "corp");
        await manager.InstallAsync(FixtureId, PackageIds.PluginPackageType, source: "http://user:" + Secret + "@feed.invalid/v3/index.json");
        await manager.InstallAsync("http://user:" + Secret + "@packages.invalid/fixture.nupkg?sig=" + Secret, PackageIds.PluginPackageType);

        Assert.IsEmpty(handler.RecordedRequests);
        Assert.IsGreaterThanOrEqualTo(4, logger.Messages.Count(message => message.Contains(".invalid/", StringComparison.Ordinal)));
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("http://feed.invalid/v3/index.json", StringComparison.Ordinal)));
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("http://packages.invalid/fixture.nupkg", StringComparison.Ordinal)));
        Assert.IsFalse(logger.Messages.Any(message => message.Contains(Secret, StringComparison.Ordinal)), string.Join(Environment.NewLine, logger.Messages));
    }

    [TestMethod]
    public async Task RefreshScan_InsecureSourceWithCredentials_LogsRedactedUrl()
    {
        using var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var logger = new RecordingLogger<RefreshCommand>();
        var source = new NuGetSource { Name = "corp", Url = "http://user:" + Secret + "@feed.invalid/v3/index.json?sig=" + Secret };

        var packages = await RefreshCommand.ScanSourceAsync(source, "remote", httpClient, logger, CancellationToken.None);

        Assert.IsEmpty(packages);
        Assert.IsEmpty(handler.RecordedRequests);
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("http://feed.invalid/v3/index.json", StringComparison.Ordinal)));
        Assert.IsFalse(logger.Messages.Any(message => message.Contains(Secret, StringComparison.Ordinal)));
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
