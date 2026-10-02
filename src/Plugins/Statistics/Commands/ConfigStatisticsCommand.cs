using System.CommandLine;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugins.Statistics.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Configuration.Keys;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Plugins.Statistics.Commands;

/// <summary>
/// Command to configure statistics plugin settings.
/// </summary>
/// <remarks>
/// <para>
/// Allows interactive or argument-based configuration of the statistics plugin.
/// Stores configuration in project.json under the plugin's section.
/// </para>
/// <para>
/// Usage: revela config statistics [options]
/// </para>
/// </remarks>
internal sealed partial class ConfigStatisticsCommand(
    ILogger<ConfigStatisticsCommand> logger,
    IConfigService configService,
    IOptionsMonitor<StatisticsPluginConfig> configMonitor,
    IConsoleCapabilities consoleCapabilities)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("statistics", "Configure statistics plugin settings");

        var maxEntriesOption = new Option<int?>("--max-entries", "-m")
        {
            Description = "Maximum entries per category (0 = unlimited)"
        };
        var sortByCountOption = new Option<bool?>("--sort-by-count", "-s")
        {
            Description = "Sort by count instead of alphabetically"
        };

        command.Options.Add(maxEntriesOption);
        command.Options.Add(sortByCountOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var maxEntries = parseResult.GetValue(maxEntriesOption);
            var sortByCount = parseResult.GetValue(sortByCountOption);

            return await ExecuteAsync(maxEntries, sortByCount, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(int? maxEntriesArg, bool? sortByCountArg, CancellationToken cancellationToken)
    {
        // Read current values from IOptions
        var current = configMonitor.CurrentValue;

        // No arguments means: ask. That needs a terminal; never prompt in CI or pipes.
        var promptForValues = maxEntriesArg is null && sortByCountArg is null;
        if (promptForValues && !consoleCapabilities.IsInteractive)
        {
            ErrorPanels.ShowError(
                "Settings Required",
                "This console is not interactive, so Revela cannot ask for the settings.\n\n" +
                "Pass them as options, for example:\n" +
                "  [cyan]revela config statistics --max-entries 20 --sort-by-count true[/]");
            return 1;
        }

        int maxEntries;
        bool sortByCount;

        if (promptForValues)
        {
            AnsiConsole.MarkupLine("[cyan]Configure Statistics Plugin[/]\n");

            maxEntries = AnsiConsole.Prompt(
                new TextPrompt<int>("Max entries per category (0 = unlimited):")
                    .DefaultValue(current.MaxEntriesPerCategory));

            sortByCount = await AnsiConsole.ConfirmAsync(
                "Sort by count (descending)?",
                current.SortByCount,
                cancellationToken);
        }
        else
        {
            // Use provided arguments or current values
            maxEntries = maxEntriesArg ?? current.MaxEntriesPerCategory;
            sortByCount = sortByCountArg ?? current.SortByCount;
        }

        // Build plugin config object
        var pluginConfig = new JsonObject
        {
            [StatisticsPluginConfigKeys.MaxEntriesPerCategory] = maxEntries,
            [StatisticsPluginConfigKeys.SortByCount] = sortByCount
        };

        // Nest below plugins:<key> and update project.json
        var updates = PluginConfigSection.CreateUpdate(StatisticsPluginConfigKeys.Section, pluginConfig);

        await configService.UpdateProjectConfigAsync(updates, cancellationToken);

        LogConfigSaved(logger, configService.ProjectConfigPath);
        AnsiConsole.MarkupLine($"\n{OutputMarkers.Success} Configuration saved to [cyan]project.json[/]");

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Statistics config saved to {Path}")]
    private static partial void LogConfigSaved(ILogger logger, string path);
}
