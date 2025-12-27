using System.CommandLine;

using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Handles 'revela theme install' command.
/// </summary>
/// <remarks>
/// Installs themes from the package index. Before running, use
/// 'revela packages refresh' to update the index.
/// </remarks>
public sealed partial class ThemeInstallCommand(
    ILogger<ThemeInstallCommand> logger,
    IPackageIndexService packageIndexService,
    PluginManager pluginManager)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("install", "Install a theme from NuGet");

        var nameArgument = new Argument<string>("name")
        {
            Description = "Theme name (e.g., 'Lumina' for Spectara.Revela.Theme.Lumina)"
        };
        command.Arguments.Add(nameArgument);

        var versionOption = new Option<string?>("--version", "-v")
        {
            Description = "Specific version to install (optional)"
        };
        command.Options.Add(versionOption);

        var globalOption = new Option<bool>("--global", "-g")
        {
            Description = "Install globally to AppData (default: local, next to executable)"
        };
        command.Options.Add(globalOption);

        var sourceOption = new Option<string?>("--source", "-s")
        {
            Description = "NuGet source name (from 'revela plugin source list') or URL"
        };
        command.Options.Add(sourceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArgument);
            var version = parseResult.GetValue(versionOption);
            var global = parseResult.GetValue(globalOption);
            var source = parseResult.GetValue(sourceOption);

            if (string.IsNullOrEmpty(name))
            {
                ErrorPanels.ShowValidationError("Theme name is required.");
                return 1;
            }

            return await ExecuteAsync(name, version, global, source, cancellationToken);
        });

        return command;
    }

    internal async Task<int> ExecuteAsync(
        string name,
        string? version,
        bool global,
        string? source,
        CancellationToken cancellationToken)
    {
        try
        {
            // Convert short name to full package ID
            // Examples: "Lumina" → "Spectara.Revela.Theme.Lumina"
            //           "Spectara.Revela.Theme.Lumina" → unchanged
            var packageId = name.StartsWith("Spectara.Revela.", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"Spectara.Revela.Theme.{name}";

            // Check if package is in the index
            var packageEntry = await packageIndexService.FindPackageAsync(packageId, cancellationToken);

            if (packageEntry is null)
            {
                // Check if index exists
                var indexAge = packageIndexService.GetIndexAge();
                if (indexAge is null)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠[/] Package index not found.");
                    AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] first.");
                    return 1;
                }

                AnsiConsole.MarkupLine($"[red]✗[/] Package [cyan]{packageId}[/] not found in index.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update the index.");
                return 1;
            }

            // Validate package type
            if (!packageEntry.Types.Contains("RevelaTheme", StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Package [cyan]{packageId}[/] is not a theme.");
                AnsiConsole.MarkupLine($"  Package types: {string.Join(", ", packageEntry.Types)}");
                AnsiConsole.MarkupLine("  Use [cyan]revela plugin install[/] for plugins.");
                return 1;
            }

            var location = global ? "globally" : "locally";
            var sourceInfo = source is not null ? $" from [dim]{source}[/]" : "";
            AnsiConsole.MarkupLine($"[blue]Installing theme {location}:[/] [cyan]{packageId}[/]{sourceInfo}");
            LogInstallingTheme(logger, packageId, version, source);

            var success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing...", async ctx =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ctx.Status($"Downloading {packageId}...");
                    return await pluginManager.InstallAsync(packageId, version, source, global, cancellationToken);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Theme [cyan]{packageId}[/] installed successfully.");
                AnsiConsole.MarkupLine("[dim]Configure with:[/] revela config theme select");
                return 0;
            }

            AnsiConsole.MarkupLine($"[red]✗[/] Failed to install theme.");
            return 1;
        }
        catch (Exception ex)
        {
            LogInstallFailed(logger, ex);
            ErrorPanels.ShowException(ex, $"Failed to install theme '{name}'.");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing theme {PackageId} version={Version} source={Source}")]
    private static partial void LogInstallingTheme(ILogger logger, string packageId, string? version, string? source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install theme")]
    private static partial void LogInstallFailed(ILogger logger, Exception exception);
}
