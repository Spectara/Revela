using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Source.OneDrive.Commands;

/// <summary>
/// Command to configure OneDrive plugin settings.
/// </summary>
/// <remarks>
/// <para>
/// Allows interactive or argument-based configuration of the OneDrive plugin.
/// Creates or updates config/Spectara.Revela.Plugin.Source.OneDrive.json.
/// </para>
/// <para>
/// Usage: revela config source onedrive [options]
/// </para>
/// </remarks>
public sealed partial class ConfigOneDriveCommand(
    ILogger<ConfigOneDriveCommand> logger,
    IOptionsMonitor<OneDrivePluginConfig> configMonitor)
{
    private const string ConfigFolderName = "config";
    private const string PluginPackageId = "Spectara.Revela.Plugin.Source.OneDrive";
    private const string ConfigPath = $"{ConfigFolderName}/{PluginPackageId}.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("onedrive", "Configure OneDrive source plugin");

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
        // Read current values from IOptions (empty if config doesn't exist yet)
        var current = configMonitor.CurrentValue;
        var isFirstTime = !File.Exists(ConfigPath);

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
                    .AllowEmpty()
                    .Validate(url =>
                    {
                        if (string.IsNullOrWhiteSpace(url))
                        {
                            return ValidationResult.Success(); // Allow empty for now
                        }

                        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            return ValidationResult.Error("[red]URL must start with https://[/]");
                        }

                        if (!url.Contains("1drv.ms", StringComparison.OrdinalIgnoreCase) &&
                            !url.Contains("onedrive.live.com", StringComparison.OrdinalIgnoreCase))
                        {
                            return ValidationResult.Error("[red]Must be a valid OneDrive share URL[/]");
                        }

                        return ValidationResult.Success();
                    }));

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

        // Ensure config directory exists
        Directory.CreateDirectory(ConfigFolderName);

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
            [PluginPackageId] = configValues
        };

        // Write config file
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json, cancellationToken).ConfigureAwait(false);

        LogConfigSaved(logger, ConfigPath);

        // Show success panel
        var action = isFirstTime ? "created" : "updated";
        var panel = new Panel(
            $"[green]OneDrive source {action}![/]\n\n" +
            $"[bold]Configuration:[/] [cyan]{ConfigPath}[/]\n" +
            (string.IsNullOrEmpty(shareUrl) ? "" : $"[bold]Share URL:[/] [dim]{shareUrl}[/]\n") +
            $"[bold]Output directory:[/] [cyan]{output}[/]\n\n" +
            $"[bold]Next steps:[/]\n" +
            $"1. Run [cyan]revela source onedrive download[/] to fetch files\n" +
            $"2. Run [cyan]revela generate[/] to build your site")
            .WithHeader($"[bold green]{(isFirstTime ? "Created" : "Updated")}[/]")
            .WithSuccessStyle();

        AnsiConsole.Write(panel);

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OneDrive config saved to {Path}")]
    private static partial void LogConfigSaved(ILogger logger, string path);
}
