using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Generate.Commands;

/// <summary>
/// Config commands prompt when called without options. Without a terminal they must fail with
/// the options to pass instead of prompting, and must not write configuration.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class ConfigCommandsNonInteractiveTests
{
    [TestMethod]
    public async Task ConfigImage_NoOptions_FailsWithFormatsHint()
    {
        var configService = ProjectConfigService();
        var command = new ConfigImageCommand(
            NullLogger<ConfigImageCommand>.Instance,
            Options.Create(new ProjectEnvironment { Path = "project" }),
            Monitor(new ThemeConfig { Name = "Lumina" }),
            Substitute.For<IThemeRegistry>(),
            configService,
            FakeConsoleCapabilities.NonInteractive);

        var (exitCode, output) = await ConsoleCapture.RunAsync(() => command.ExecuteAsync(null, CancellationToken.None));

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela config image --formats", output);
        await AssertNothingWrittenAsync(configService);
    }

    [TestMethod]
    public async Task ConfigImage_FormatsWithoutQuality_WritesDefaultQualities()
    {
        // AVIF 75 is about 18 % smaller than WebP 85 at visually equal quality on real photos;
        // AVIF 80 was larger than WebP 85.
        var configService = ProjectConfigService();
        configService.ReadProjectConfigAsync(Arg.Any<CancellationToken>()).Returns([]);
        JsonObject? written = null;
        await configService.UpdateProjectConfigAsync(Arg.Do<JsonObject>(update => written = update), Arg.Any<CancellationToken>());
        var themeRegistry = Substitute.For<IThemeRegistry>();
        themeRegistry.Resolve(Arg.Any<string>(), Arg.Any<string>()).Returns((ITheme?)null);
        var command = new ConfigImageCommand(
            NullLogger<ConfigImageCommand>.Instance,
            Options.Create(new ProjectEnvironment { Path = "project" }),
            Monitor(new ThemeConfig { Name = "Lumina" }),
            themeRegistry,
            configService,
            FakeConsoleCapabilities.NonInteractive);

        var (exitCode, _) = await ConsoleCapture.RunAsync(() => command.ExecuteAsync("avif,webp,jpg", CancellationToken.None));

        Assert.AreEqual(0, exitCode);
        var images = written?["generate"]?["images"];
        Assert.IsNotNull(images);
        Assert.AreEqual(75, images["avif"]!.GetValue<int>());
        Assert.AreEqual(85, images["webp"]!.GetValue<int>());
        Assert.AreEqual(90, images["jpg"]!.GetValue<int>());
    }

    [TestMethod]
    public async Task ConfigSorting_NoOptions_FailsWithOptionsHint()
    {
        var configService = ProjectConfigService();
        var command = new ConfigSortingCommand(configService, FakeConsoleCapabilities.NonInteractive);

        var (exitCode, output) = await ConsoleCapture.RunAsync(
            () => command.ExecuteAsync(null, null, null, null, CancellationToken.None));

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela config sorting", output);
        Assert.Contains("--galleries", output);
        Assert.Contains("--field", output);
        await AssertNothingWrittenAsync(configService);
    }

    [TestMethod]
    public async Task ConfigSorting_OptionGiven_WritesWithoutPrompting()
    {
        var configService = ProjectConfigService();
        var command = new ConfigSortingCommand(configService, FakeConsoleCapabilities.NonInteractive);

        var (exitCode, _) = await ConsoleCapture.RunAsync(
            () => command.ExecuteAsync("desc", null, null, null, CancellationToken.None));

        Assert.AreEqual(0, exitCode);
        await configService.Received(1).UpdateProjectConfigAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ConfigPaths_NoOptions_FailsWithSourceHint()
    {
        var configService = ProjectConfigService();
        var command = new ConfigPathsCommand(
            NullLogger<ConfigPathsCommand>.Instance,
            Options.Create(new ProjectEnvironment { Path = "project" }),
            Monitor(new PathsConfig()),
            Substitute.For<IPathResolver>(),
            configService,
            FakeConsoleCapabilities.NonInteractive);

        var (exitCode, output) = await ConsoleCapture.RunAsync(() => command.ExecuteAsync(null, null, CancellationToken.None));

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela config paths --source <dir>", output);
        await AssertNothingWrittenAsync(configService);
    }

    private static IConfigService ProjectConfigService()
    {
        var configService = Substitute.For<IConfigService>();
        configService.IsProjectInitialized().Returns(true);
        return configService;
    }

    private static IOptionsMonitor<T> Monitor<T>(T value)
    {
        var monitor = Substitute.For<IOptionsMonitor<T>>();
        monitor.CurrentValue.Returns(value);
        return monitor;
    }

    private static Task AssertNothingWrittenAsync(IConfigService configService) =>
        configService.DidNotReceive().UpdateProjectConfigAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
}
