using System.CommandLine;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugins.Source.OneDrive.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Configuration.Keys;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Validation;
using Spectre.Console;

namespace Spectara.Revela.Plugins.Source.OneDrive.Commands;

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
internal sealed partial class ConfigOneDriveCommand(
    ILogger<ConfigOneDriveCommand> logger,
    IConfigService configService,
    IOptionsMonitor<OneDrivePluginConfig> configMonitor,
    IOptionsMonitor<PathsConfig> pathsConfig,
    IConsoleCapabilities consoleCapabilities)
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

            return await ExecuteAsync(shareUrl, cancellationToken);
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

        // No arguments means: ask. That needs a terminal; never prompt in CI or pipes.
        var promptForValues = shareUrlArg is null;
        if (promptForValues && !consoleCapabilities.IsInteractive)
        {
            ErrorPanels.ShowError(
                "Settings Required",
                "This console is not interactive, so Revela cannot ask for the settings.\n\n" +
                "Pass them as options, for example:\n" +
                "  [cyan]revela config onedrive --share-url https://1drv.ms/f/...[/]");
            return 1;
        }

        string shareUrl;

        if (promptForValues)
        {
            AnsiConsole.MarkupLine("[cyan]Configure OneDrive Source Plugin[/]\n");

            shareUrl = AnsiConsole.Prompt(
                new TextPrompt<string>("OneDrive share URL:")
                    .DefaultValue(current.ShareUrl)
                    .AllowEmpty()
                    .Validate(url => ValidateShareUrl(url) is { } error
                        ? ValidationResult.Error($"[red]{Markup.Escape(error)}[/]")
                        : ValidationResult.Success()));
        }
        else
        {
            shareUrl = shareUrlArg!;
            if (ValidateShareUrl(shareUrl) is { } error)
            {
                ErrorPanels.ShowError("Invalid Share URL", Markup.Escape(error));
                return 1;
            }
        }
        // Build config object (only include non-default values)
        var pluginConfig = new JsonObject();

        if (!string.IsNullOrEmpty(shareUrl))
        {
            pluginConfig[OneDrivePluginConfigKeys.ShareUrl] = shareUrl;
        }

        // Nest below plugins:<key> and update project.json
        var updates = PluginConfigSection.CreateUpdate(OneDrivePluginConfigKeys.Section, pluginConfig);

        await configService.UpdateProjectConfigAsync(updates, cancellationToken);

        LogConfigSaved(configService.ProjectConfigPath);

        // Show success panel
        var action = isFirstTime ? "created" : "updated";
        var sourceDir = pathsConfig.CurrentValue.Source;
        var panel = new Panel(
            $"[green]OneDrive source {action}![/]\n\n" +
            $"[bold]Configuration:[/] [cyan]project.json[/]\n" +
            (string.IsNullOrEmpty(shareUrl) ? "" : $"[bold]Share URL:[/] [dim]{Markup.Escape(shareUrl)}[/]\n") +
            $"[bold]Output directory:[/] [cyan]{Markup.Escape(sourceDir)}/[/]\n\n" +
            $"[bold]Next steps:[/]\n" +
            $"1. Run [cyan]revela source onedrive sync[/] to fetch files\n" +
            $"2. Run [cyan]revela generate all[/] to build your site")
            .WithHeader($"[bold green]{(isFirstTime ? "Created" : "Updated")}[/]")
            .WithSuccessStyle();

        AnsiConsole.Write(panel);

        return 0;
    }

    /// <summary>
    /// Returns why <paramref name="url"/> is not an acceptable share URL, or <see langword="null"/>
    /// when it is acceptable. An empty value is accepted (no share configured yet).
    /// </summary>
    internal static string? ValidateShareUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "Not a valid URL.";
        }

        if (!UrlSafety.IsSafeOutboundUrl(uri))
        {
            return "The URL must use https and may not point to loopback, private, or link-local addresses.";
        }

        // Host equality, not Contains: Contains would accept attacker.com/?fake=1drv.ms.
        if (!string.Equals(uri.Host, "1drv.ms", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "onedrive.live.com", StringComparison.OrdinalIgnoreCase))
        {
            return "The host must be 1drv.ms or onedrive.live.com.";
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OneDrive config saved to {Path}")]
    private partial void LogConfigSaved(string path);
}
