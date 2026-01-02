using System.CommandLine;

using Spectara.Revela.Core;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Handles 'revela theme uninstall' command.
/// </summary>
/// <remarks>
/// Uninstalls themes from the local or global plugin directory.
/// Themes are installed as plugin packages, so uninstall works the same way.
/// </remarks>
public sealed partial class ThemeUninstallCommand(
    ILogger<ThemeUninstallCommand> logger,
    PluginManager pluginManager)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("uninstall", "Uninstall a theme");

        var nameArgument = new Argument<string>("name")
        {
            Description = "Theme name to uninstall (e.g., 'Lumina' or 'Spectara.Revela.Theme.Lumina')"
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

            if (string.IsNullOrEmpty(name))
            {
                ErrorPanels.ShowValidationError("Theme name is required.");
                return 1;
            }

            return await ExecuteAsync(name, skipConfirm, cancellationToken);
        });

        return command;
    }

    internal async Task<int> ExecuteAsync(
        string name,
        bool skipConfirm = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert short name to full package ID
            // Examples: "Lumina" → "Spectara.Revela.Theme.Lumina"
            //           "Spectara.Revela.Theme.Lumina" → unchanged
            var packageId = name.StartsWith("Spectara.Revela.", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"Spectara.Revela.Theme.{name}";

            if (!skipConfirm && !await AnsiConsole.ConfirmAsync(
                $"[yellow]Uninstall theme '{packageId}'?[/]",
                defaultValue: false,
                cancellationToken))
            {
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[blue]Uninstalling theme:[/] [cyan]{packageId}[/]");
            LogUninstallingTheme(logger, packageId);

            var success = await pluginManager.UninstallPluginAsync(packageId, cancellationToken: cancellationToken);

            if (success)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} Theme [cyan]{packageId}[/] uninstalled successfully.");
                return 0;
            }

            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Theme [cyan]{packageId}[/] not found.");
            return 1;
        }
        catch (Exception ex)
        {
            LogUninstallFailed(logger, ex);
            ErrorPanels.ShowException(ex, $"Failed to uninstall theme '{name}'.");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling theme {PackageId}")]
    private static partial void LogUninstallingTheme(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to uninstall theme")]
    private static partial void LogUninstallFailed(ILogger logger, Exception exception);
}
