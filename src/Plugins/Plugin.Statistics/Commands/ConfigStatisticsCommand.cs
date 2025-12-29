using System.CommandLine;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Statistics.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Statistics.Commands;

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
public sealed partial class ConfigStatisticsCommand(
    ILogger<ConfigStatisticsCommand> logger,
    IConfigService configService,
    IOptionsMonitor<StatisticsPluginConfig> configMonitor)
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

            return await ExecuteAsync(maxEntries, sortByCount, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(int? maxEntriesArg, bool? sortByCountArg, CancellationToken cancellationToken)
    {
        // Read current values from IOptions
        var current = configMonitor.CurrentValue;

        // Determine if interactive mode (no arguments provided)
        var isInteractive = maxEntriesArg is null && sortByCountArg is null;

        int maxEntries;
        bool sortByCount;

        if (isInteractive)
        {
            AnsiConsole.MarkupLine("[cyan]Configure Statistics Plugin[/]\n");

            maxEntries = AnsiConsole.Prompt(
                new TextPrompt<int>("Max entries per category (0 = unlimited):")
                    .DefaultValue(current.MaxEntriesPerCategory));

            sortByCount = await AnsiConsole.ConfirmAsync(
                "Sort by count (descending)?",
                current.SortByCount,
                cancellationToken).ConfigureAwait(false);
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
            ["MaxEntriesPerCategory"] = maxEntries,
            ["SortByCount"] = sortByCount
        };

        // Wrap with plugin section name and update project.json
        var updates = new JsonObject
        {
            [StatisticsPluginConfig.SectionName] = pluginConfig
        };

        await configService.UpdateProjectConfigAsync(updates, cancellationToken).ConfigureAwait(false);

        LogConfigSaved(logger, configService.ProjectConfigPath);
        AnsiConsole.MarkupLine($"\n[green]✓[/] Configuration saved to [cyan]project.json[/]");

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Statistics config saved to {Path}")]
    private static partial void LogConfigSaved(ILogger logger, string path);
}
