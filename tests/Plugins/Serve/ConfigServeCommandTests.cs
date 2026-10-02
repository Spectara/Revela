using System.Globalization;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Plugins.Serve;
using Spectara.Revela.Plugins.Serve.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;

using Spectre.Console;

namespace Spectara.Revela.Tests.Plugins.Serve;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class ConfigServeCommandTests
{
    [TestMethod]
    public async Task Invoke_NoArgumentsOnNonInteractiveConsole_FailsWithoutPromptingOrSaving()
    {
        var configService = Substitute.For<IConfigService>();
        var command = CreateCommand(configService, isInteractive: false);

        var (exitCode, output) = await InvokeAsync(command, []);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("--port", output, StringComparison.Ordinal);
        await configService.DidNotReceive().UpdateProjectConfigAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Invoke_ArgumentsOnNonInteractiveConsole_SavesConfig()
    {
        var configService = Substitute.For<IConfigService>();
        var command = CreateCommand(configService, isInteractive: false);

        var (exitCode, _) = await InvokeAsync(command, ["--port", "3000"]);

        Assert.AreEqual(0, exitCode);
        await configService.Received(1).UpdateProjectConfigAsync(
            Arg.Is<JsonObject>(updates => updates.ToJsonString().Contains("3000", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    private static ConfigServeCommand CreateCommand(IConfigService configService, bool isInteractive)
    {
        var monitor = Substitute.For<IOptionsMonitor<ServePluginConfig>>();
        monitor.CurrentValue.Returns(new ServePluginConfig());
        var console = Substitute.For<IConsoleCapabilities>();
        console.IsInteractive.Returns(isInteractive);
        console.CanRenderLive.Returns(isInteractive);
        return new ConfigServeCommand(NullLogger<ConfigServeCommand>.Instance, configService, monitor, console);
    }

    private static async Task<(int ExitCode, string Output)> InvokeAsync(ConfigServeCommand command, string[] args)
    {
        var originalConsole = AnsiConsole.Console;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
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
