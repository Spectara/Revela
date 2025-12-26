using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Serve.Configuration;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Serve;

/// <summary>
/// Command to configure serve plugin settings.
/// </summary>
/// <remarks>
/// <para>
/// Allows interactive or argument-based configuration of the serve plugin.
/// Reads/writes to config/Spectara.Revela.Plugin.Serve.json.
/// </para>
/// <para>
/// Usage: revela config serve [options]
/// </para>
/// </remarks>
public sealed partial class ConfigServeCommand(
    ILogger<ConfigServeCommand> logger,
    IOptionsMonitor<ServeConfig> configMonitor)
{
    private const string ConfigPath = "config/Spectara.Revela.Plugin.Serve.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

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

            return await ExecuteAsync(port, verbose, cancellationToken).ConfigureAwait(false);
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
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Use provided arguments or current values
            port = portArg ?? current.Port;
            verbose = verboseArg ?? current.Verbose;
        }

        // Ensure plugins directory exists
        Directory.CreateDirectory("plugins");

        // Build config object
        var config = new Dictionary<string, object>
        {
            [ServeConfig.SectionName] = new Dictionary<string, object>
            {
                ["Port"] = port,
                ["Verbose"] = verbose
            }
        };

        // Write config file
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json, cancellationToken).ConfigureAwait(false);

        LogConfigSaved(logger, ConfigPath);
        AnsiConsole.MarkupLine($"\n[green]✓[/] Configuration saved to [cyan]{ConfigPath}[/]");

        // Show summary
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Port", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        table.AddRow("Verbose", verbose ? "Yes" : "No");

        AnsiConsole.Write(table);

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Serve config saved to {Path}")]
    private static partial void LogConfigSaved(ILogger logger, string path);
}
