using System.CommandLine;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Serve.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Serve;

/// <summary>
/// Command to configure serve plugin settings.
/// </summary>
/// <remarks>
/// <para>
/// Allows interactive or argument-based configuration of the serve plugin.
/// Stores configuration in project.json under the plugin's section.
/// </para>
/// <para>
/// Usage: revela config serve [options]
/// </para>
/// </remarks>
public sealed partial class ConfigServeCommand(
    ILogger<ConfigServeCommand> logger,
    IConfigService configService,
    IOptionsMonitor<ServeConfig> configMonitor)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("serve", "Configure serve plugin settings");

        var portOption = new Option<int?>("--port", "-p")
        {
            Description = "Port number for the HTTP server"
        };
        var verboseOption = new Option<bool?>("--verbose", "-v")
        {
            Description = "Log all requests (not just 404s)"
        };

        command.Options.Add(portOption);
        command.Options.Add(verboseOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var port = parseResult.GetValue(portOption);
            var verbose = parseResult.GetValue(verboseOption);

            return await ExecuteAsync(port, verbose, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(int? portArg, bool? verboseArg, CancellationToken cancellationToken)
    {
        // Read current values from IOptions
        var current = configMonitor.CurrentValue;

        // Determine if interactive mode (no arguments provided)
        var isInteractive = portArg is null && verboseArg is null;

        int port;
        bool verbose;

        if (isInteractive)
        {
            AnsiConsole.MarkupLine("[cyan]Configure Serve Plugin[/]\n");

            port = AnsiConsole.Prompt(
                new TextPrompt<int>("Port number:")
                    .DefaultValue(current.Port)
                    .Validate(p => p is >= 1 and <= 65535
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Port must be between 1 and 65535")));

            verbose = await AnsiConsole.ConfirmAsync(
                "Enable verbose logging (all requests)?",
                current.Verbose,
                cancellationToken);
        }
        else
        {
            // Use provided arguments or current values
            port = portArg ?? current.Port;
            verbose = verboseArg ?? current.Verbose;
        }

        // Build plugin config object
        var pluginConfig = new JsonObject
        {
            ["Port"] = port,
            ["Verbose"] = verbose
        };

        // Wrap with plugin section name and update project.json
        var updates = new JsonObject
        {
            [ServeConfig.SectionName] = pluginConfig
        };

        await configService.UpdateProjectConfigAsync(updates, cancellationToken);

        LogConfigSaved(logger, configService.ProjectConfigPath);
        AnsiConsole.MarkupLine($"\n{OutputMarkers.Success} Configuration saved to [cyan]project.json[/]");

        // Show summary
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Port", port.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Verbose", verbose ? "Yes" : "No");

        AnsiConsole.Write(table);

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Serve config saved to {Path}")]
    private static partial void LogConfigSaved(ILogger logger, string path);
}
