using System.CommandLine;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Source.OneDrive.Commands;

/// <summary>
/// Command to configure OneDrive plugin settings.
/// </summary>
/// <remarks>
/// <para>
/// Allows interactive or argument-based configuration of the OneDrive plugin.
/// Stores configuration in project.json under the plugin's section.
/// </para>
/// <para>
/// Usage: revela config source onedrive [options]
/// </para>
/// </remarks>
public sealed partial class ConfigOneDriveCommand(
    ILogger<ConfigOneDriveCommand> logger,
    IConfigService configService,
    IOptionsMonitor<OneDrivePluginConfig> configMonitor)
{
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

        command.Options.Add(shareUrlOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var shareUrl = parseResult.GetValue(shareUrlOption);

            return await ExecuteAsync(shareUrl, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes the configuration in interactive mode (no arguments).
    /// </summary>
    /// <remarks>
    /// Used by <see cref="Wizard.OneDriveWizardStep"/> to run configuration
    /// as part of the project setup wizard.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code: 0 = success, non-zero = error.</returns>
    public Task<int> ExecuteInteractiveAsync(CancellationToken cancellationToken)
        => ExecuteAsync(null, cancellationToken);

    private async Task<int> ExecuteAsync(
        string? shareUrlArg,
        CancellationToken cancellationToken)
    {
        // Read current values from IOptions (empty if config doesn't exist yet)
        var current = configMonitor.CurrentValue;

        // Check if plugin is already configured by looking for non-default ShareUrl
        var isFirstTime = string.IsNullOrEmpty(current.ShareUrl);

        // Determine if interactive mode (no arguments provided)
        var isInteractive = shareUrlArg is null;

        string shareUrl;

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
        }
        else
        {
            // Use provided argument or current value
            shareUrl = shareUrlArg ?? current.ShareUrl;
        }

        // Build config object (only include non-default values)
        var pluginConfig = new JsonObject();

        if (!string.IsNullOrEmpty(shareUrl))
        {
            pluginConfig["ShareUrl"] = shareUrl;
        }

        // Wrap with plugin section name and update project.json
        var updates = new JsonObject
        {
            [OneDrivePluginConfig.SectionName] = pluginConfig
        };

        await configService.UpdateProjectConfigAsync(updates, cancellationToken).ConfigureAwait(false);

        LogConfigSaved(logger, configService.ProjectConfigPath);

        // Show success panel
        var action = isFirstTime ? "created" : "updated";
        var panel = new Panel(
            $"[green]OneDrive source {action}![/]\n\n" +
            $"[bold]Configuration:[/] [cyan]project.json[/]\n" +
            (string.IsNullOrEmpty(shareUrl) ? "" : $"[bold]Share URL:[/] [dim]{shareUrl}[/]\n") +
            $"[bold]Output directory:[/] [cyan]{ProjectPaths.Source}/[/]\n\n" +
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
