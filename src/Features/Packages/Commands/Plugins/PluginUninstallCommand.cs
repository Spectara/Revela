using System.CommandLine;

using Spectara.Revela.Core;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Handles 'revela plugin uninstall' command.
/// </summary>
internal sealed partial class PluginUninstallCommand(
    ILogger<PluginUninstallCommand> logger,
    PackageManager pluginManager)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("uninstall", "Uninstall a plugin");

        var nameArgument = new Argument<string>("name")
        {
            Description = "Plugin name to uninstall"
        };
        command.Arguments.Add(nameArgument);

        var yesOption = new Option<bool>("--yes", "-y")
        {
            Description = "Skip confirmation prompt"
        };
        command.Options.Add(yesOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArgument);
            var skipConfirm = parseResult.GetValue(yesOption);
            return await ExecuteAsync(name!, skipConfirm, cancellationToken);
        });

        return command;
    }

    internal async Task<int> ExecuteAsync(string name, bool skipConfirm = false, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert short name to full package ID
            // Examples: "OneDrive" → "Spectara.Revela.Plugins.OneDrive"
            //           "Spectara.Revela.Plugins.OneDrive" → unchanged
            //           "Spectara.Revela.Themes.Lumina.Statistics" → unchanged
            var packageId = name.StartsWith("Spectara.Revela.", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"Spectara.Revela.Plugins.{name}";

            if (!skipConfirm && !await AnsiConsole.ConfirmAsync($"[yellow]Uninstall plugin '{packageId}'?[/]", defaultValue: false, cancellationToken))
            {
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"{OutputMarkers.Info} Uninstalling plugin: [cyan]{Markup.Escape(packageId)}[/]");
            LogUninstallingPlugin(packageId);

            var success = await pluginManager.UninstallPluginAsync(packageId, cancellationToken: cancellationToken);

            if (success)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} Plugin [cyan]{Markup.Escape(packageId)}[/] uninstalled successfully.");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Plugin [cyan]{Markup.Escape(packageId)}[/] not found.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            ErrorPanels.ShowException(ex);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling plugin '{PackageId}'")]
    private partial void LogUninstallingPlugin(string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to uninstall plugin")]
    private partial void LogError(Exception exception);
}

