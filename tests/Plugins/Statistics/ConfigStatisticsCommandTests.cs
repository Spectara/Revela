using System.Globalization;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Plugins.Statistics.Commands;
using Spectara.Revela.Plugins.Statistics.Configuration;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Configuration.Keys;
using Spectara.Revela.Sdk.Hosting;

using Spectre.Console;

namespace Spectara.Revela.Tests.Plugins.Statistics;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class ConfigStatisticsCommandTests
{
    [TestMethod]
    public async Task Invoke_NoArgumentsOnNonInteractiveConsole_FailsWithoutPromptingOrSaving()
    {
        var settingsWriter = Substitute.For<IPluginSettingsWriter<StatisticsPluginConfig>>();
        var command = CreateCommand(settingsWriter, isInteractive: false);

        var (exitCode, output) = await InvokeAsync(command, []);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("--max-entries", output, StringComparison.Ordinal);
        await settingsWriter.DidNotReceive().WriteAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Invoke_ArgumentsOnNonInteractiveConsole_SavesConfig()
    {
        var settingsWriter = Substitute.For<IPluginSettingsWriter<StatisticsPluginConfig>>();
        var command = CreateCommand(settingsWriter, isInteractive: false);

        var (exitCode, _) = await InvokeAsync(command, ["--max-entries", "42"]);

        Assert.AreEqual(0, exitCode);
        await settingsWriter.Received(1).WriteAsync(
            Arg.Is<JsonObject>(settings => (int?)settings[StatisticsPluginConfigKeys.MaxEntriesPerCategory] == 42),
            Arg.Any<CancellationToken>());
    }

    private static ConfigStatisticsCommand CreateCommand(IPluginSettingsWriter<StatisticsPluginConfig> settingsWriter, bool isInteractive)
    {
        var monitor = Substitute.For<IOptionsMonitor<StatisticsPluginConfig>>();
        monitor.CurrentValue.Returns(new StatisticsPluginConfig());
        var console = Substitute.For<IConsoleCapabilities>();
        console.IsInteractive.Returns(isInteractive);
        console.CanRenderLive.Returns(isInteractive);
        return new ConfigStatisticsCommand(NullLogger<ConfigStatisticsCommand>.Instance, settingsWriter, monitor, console);
    }

    private static async Task<(int ExitCode, string Output)> InvokeAsync(ConfigStatisticsCommand command, string[] args)
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
