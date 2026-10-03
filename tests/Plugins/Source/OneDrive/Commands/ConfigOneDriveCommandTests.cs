using System.Globalization;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Plugins.Source.OneDrive.Commands;
using Spectara.Revela.Plugins.Source.OneDrive.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;

using Spectre.Console;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Commands;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class ConfigOneDriveCommandTests
{
    [TestMethod]
    public async Task Invoke_NoArgumentsOnNonInteractiveConsole_FailsWithoutPromptingOrSaving()
    {
        var configService = Substitute.For<IConfigService>();
        var command = CreateCommand(configService, isInteractive: false);

        var (exitCode, output) = await InvokeAsync(command, []);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("--share-url", output, StringComparison.Ordinal);
        await configService.DidNotReceive().UpdateProjectConfigAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Invoke_ValidShareUrlOnNonInteractiveConsole_SavesConfig()
    {
        var configService = Substitute.For<IConfigService>();
        var command = CreateCommand(configService, isInteractive: false);

        var (exitCode, _) = await InvokeAsync(command, ["--share-url", "https://1drv.ms/f/s!example"]);

        Assert.AreEqual(0, exitCode);
        await configService.Received(1).UpdateProjectConfigAsync(
            Arg.Is<JsonObject>(updates => updates.ToJsonString().Contains("1drv.ms", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow("https://attacker.example/?fake=1drv.ms")]
    [DataRow("http://1drv.ms/f/s!example")]
    [DataRow("not a url")]
    public async Task Invoke_UnsafeShareUrlArgument_FailsWithoutSaving(string argument)
    {
        var configService = Substitute.For<IConfigService>();
        var command = CreateCommand(configService, isInteractive: false);

        var (exitCode, _) = await InvokeAsync(command, ["--share-url", argument]);

        Assert.AreNotEqual(0, exitCode);
        await configService.DidNotReceive().UpdateProjectConfigAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
    }

    private static ConfigOneDriveCommand CreateCommand(IConfigService configService, bool isInteractive)
    {
        var monitor = Substitute.For<IOptionsMonitor<OneDrivePluginConfig>>();
        monitor.CurrentValue.Returns(new OneDrivePluginConfig());
        var paths = Substitute.For<IOptionsMonitor<PathsConfig>>();
        paths.CurrentValue.Returns(new PathsConfig());
        var console = Substitute.For<IConsoleCapabilities>();
        console.IsInteractive.Returns(isInteractive);
        console.CanRenderLive.Returns(isInteractive);
        return new ConfigOneDriveCommand(NullLogger<ConfigOneDriveCommand>.Instance, configService, monitor, paths, console);
    }

    private static async Task<(int ExitCode, string Output)> InvokeAsync(ConfigOneDriveCommand command, string[] args)
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
            var exitCode = await command.Create().Parse(args).InvokeAsync();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
