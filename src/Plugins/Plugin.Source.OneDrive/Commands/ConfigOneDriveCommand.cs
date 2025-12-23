using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Source.OneDrive.Commands;

/// <summary>
/// Command to configure OneDrive plugin settings.
/// </summary>
/// <remarks>
/// <para>
/// Allows interactive or argument-based configuration of the OneDrive plugin.
/// Reads/writes to plugins/Spectara.Revela.Plugin.Source.OneDrive.json.
/// </para>
/// <para>
/// Usage: revela config onedrive [options]
/// </para>
/// </remarks>
public sealed partial class ConfigOneDriveCommand(
    ILogger<ConfigOneDriveCommand> logger,
    IOptionsMonitor<OneDrivePluginConfig> configMonitor)
{
    private const string ConfigPath = "plugins/Spectara.Revela.Plugin.Source.OneDrive.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("onedrive", "Configure OneDrive source plugin settings");

        var shareUrlOption = new Option<string?>("--share-url", "-u")
        {
            Description = "OneDrive shared folder URL"
        };
        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output directory for downloaded files"
        };
        var concurrencyOption = new Option<int?>("--concurrency", "-c")
        {
            Description = "Number of parallel downloads"
        };

        command.Options.Add(shareUrlOption);
        command.Options.Add(outputOption);
        command.Options.Add(concurrencyOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var shareUrl = parseResult.GetValue(shareUrlOption);
            var output = parseResult.GetValue(outputOption);
            var concurrency = parseResult.GetValue(concurrencyOption);

            return await ExecuteAsync(shareUrl, output, concurrency, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(
        string? shareUrlArg,
        string? outputArg,
        int? concurrencyArg,
        CancellationToken cancellationToken)
    {
        // Read current values from IOptions
        var current = configMonitor.CurrentValue;

        // Determine if interactive mode (no arguments provided)
        var isInteractive = shareUrlArg is null && outputArg is null && concurrencyArg is null;

        string shareUrl;
        string output;
        int? concurrency;

        if (isInteractive)
        {
            AnsiConsole.MarkupLine("[cyan]Configure OneDrive Source Plugin[/]\n");

            shareUrl = AnsiConsole.Prompt(
                new TextPrompt<string>("OneDrive share URL:")
                    .DefaultValue(current.ShareUrl)
                    .AllowEmpty());

            output = AnsiConsole.Prompt(
                new TextPrompt<string>("Output directory:")
                    .DefaultValue(current.OutputDirectory));

            var defaultConcurrency = current.DefaultConcurrency ?? Environment.ProcessorCount;
            var concurrencyInput = AnsiConsole.Prompt(
                new TextPrompt<int>("Parallel downloads:")
                    .DefaultValue(defaultConcurrency));
            concurrency = concurrencyInput;
        }
        else
        {
            // Use provided arguments or current values
            shareUrl = shareUrlArg ?? current.ShareUrl;
            output = outputArg ?? current.OutputDirectory;
            concurrency = concurrencyArg ?? current.DefaultConcurrency;
        }

        // Ensure plugins directory exists
        Directory.CreateDirectory("plugins");

        // Build config object (only include non-default values)
        var configValues = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(shareUrl))
        {
            configValues["ShareUrl"] = shareUrl;
        }

        if (output != "source")
        {
            configValues["OutputDirectory"] = output;
        }

        if (concurrency.HasValue)
        {
            configValues["DefaultConcurrency"] = concurrency.Value;
        }

        var config = new Dictionary<string, object>
        {
            [OneDrivePluginConfig.SectionName] = configValues
        };

        // Write config file
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json, cancellationToken).ConfigureAwait(false);

        LogConfigSaved(logger, ConfigPath);
        AnsiConsole.MarkupLine($"\n[green]✓[/] Configuration saved to [cyan]{ConfigPath}[/]");

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OneDrive config saved to {Path}")]
    private static partial void LogConfigSaved(ILogger logger, string path);
}
