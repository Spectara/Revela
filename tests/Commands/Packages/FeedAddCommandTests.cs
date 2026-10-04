using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Commands.Config.Feed;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class FeedAddCommandTests
{
    [TestMethod]
    [DataRow("http://feed.example/v3/index.json")]
    [DataRow("HTTP://feed.example/v3/index.json")]
    [DataRow("ftp://feed.example/packages")]
    public async Task Add_InsecureOrUnsupportedUrl_FailsAndSavesNothing(string location)
    {
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new AddCommand(NullLogger<AddCommand>.Instance, globalConfig).Create();

        var (exitCode, output) = await ConsoleCapture.InvokeAsync(command, "private", location);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("https://", output);
        Assert.Contains(location, output);
        Assert.IsEmpty(globalConfig.ReceivedCalls());
    }

    [TestMethod]
    [DataRow("https://feed.example/v3/index.json")]
    [DataRow("http://localhost:5555/v3/index.json")]
    [DataRow("http://127.0.0.1:5555/v3/index.json")]
    [DataRow("local-feed")]
    [DataRow("../shared/feed")]
    public async Task Add_HttpsLoopbackOrFolder_SavesFeed(string location)
    {
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new AddCommand(NullLogger<AddCommand>.Instance, globalConfig).Create();

        var (exitCode, _) = await ConsoleCapture.InvokeAsync(command, "private", location);

        Assert.AreEqual(0, exitCode);
        await globalConfig.Received(1).AddFeedAsync("private", location, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Add_AbsoluteFolderPath_SavesFeed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "revela-feed");
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new AddCommand(NullLogger<AddCommand>.Instance, globalConfig).Create();

        var (exitCode, _) = await ConsoleCapture.InvokeAsync(command, "private", folder);

        Assert.AreEqual(0, exitCode);
        await globalConfig.Received(1).AddFeedAsync("private", folder, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Add_UncPath_SavesFeedOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("UNC paths are a Windows feature.");
        }

        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new AddCommand(NullLogger<AddCommand>.Instance, globalConfig).Create();

        var (exitCode, _) = await ConsoleCapture.InvokeAsync(command, "private", @"\\server\share\feed");

        Assert.AreEqual(0, exitCode);
        await globalConfig.Received(1).AddFeedAsync("private", @"\\server\share\feed", Arg.Any<CancellationToken>());
    }
}
