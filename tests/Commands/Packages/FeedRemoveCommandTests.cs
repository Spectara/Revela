using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Commands.Config.Feed;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class FeedRemoveCommandTests
{
    [TestMethod]
    public async Task Remove_NoNameNonInteractive_FailsWithHintAndRemovesNothing()
    {
        var sourceManager = Substitute.For<INuGetSourceManager>();
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        var command = new RemoveCommand(
            NullLogger<RemoveCommand>.Instance,
            sourceManager,
            globalConfig,
            FakeConsoleCapabilities.NonInteractive).Create();

        var (exitCode, output) = await ConsoleCapture.InvokeAsync(command);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela config feed remove <name>", output);
        Assert.IsEmpty(globalConfig.ReceivedCalls());
    }

    [TestMethod]
    public async Task Remove_NameGivenNonInteractive_RemovesWithoutPrompting()
    {
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        globalConfig.RemoveFeedAsync("private", Arg.Any<CancellationToken>()).Returns(true);
        var command = new RemoveCommand(
            NullLogger<RemoveCommand>.Instance,
            Substitute.For<INuGetSourceManager>(),
            globalConfig,
            FakeConsoleCapabilities.NonInteractive).Create();

        var (exitCode, _) = await ConsoleCapture.InvokeAsync(command, "private");

        Assert.AreEqual(0, exitCode);
        await globalConfig.Received(1).RemoveFeedAsync("private", Arg.Any<CancellationToken>());
    }
}
