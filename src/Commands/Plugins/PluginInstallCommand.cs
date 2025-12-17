using System.CommandLine;
using Spectara.Revela.Core;
using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Handles 'revela plugin install' command.
/// </summary>
public sealed partial class PluginInstallCommand(
    ILogger<PluginInstallCommand> logger,
    PluginManager pluginManager)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("install", "Install a plugin from NuGet");

        var nameArgument = new Argument<string?>("name")
        {
            Description = "Plugin name (e.g., 'onedrive' for Revela.Plugin.OneDrive)",
            Arity = ArgumentArity.ZeroOrOne
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

        command.SetAction(async parseResult =>
        {
            var name = parseResult.GetValue(nameArgument);
            var version = parseResult.GetValue(versionOption);
            var global = parseResult.GetValue(globalOption);
            var source = parseResult.GetValue(sourceOption);

            if (string.IsNullOrEmpty(name))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Plugin name is required");
                return 1;
            }

            return await ExecuteFromNuGetAsync(name, version, global, source);
        });

        return command;
    }

    internal async Task<int> ExecuteFromNuGetAsync(string name, string? version, bool global, string? source = null)
    {
        try
        {
            // Convert short name to full package ID
            // Examples: "OneDrive" → "Spectara.Revela.Plugin.OneDrive"
            //           "Source.OneDrive" → "Spectara.Revela.Plugin.Source.OneDrive"
            //           "Spectara.Revela.Plugin.OneDrive" → unchanged
            //           "Spectara.Revela.Theme.Lumina.Statistics" → unchanged
            var packageId = name.StartsWith("Spectara.Revela.", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"Spectara.Revela.Plugin.{name}";

            var location = global ? "globally" : "locally";
            var sourceInfo = source is not null ? $" from [dim]{source}[/]" : "";
            AnsiConsole.MarkupLine($"[blue]Installing plugin {location}:[/] [cyan]{packageId}[/]{sourceInfo}");
            LogInstallingPlugin(packageId, version, source);

            var success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing...", async ctx =>
                {
                    ctx.Status($"Downloading {packageId}...");
                    return await pluginManager.InstallAsync(packageId, version, source, global);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]Plugin '{packageId}' installed successfully.[/]");
                AnsiConsole.MarkupLine("[dim]The plugin will be available after restarting revela.[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Failed to install plugin '{packageId}'[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin '{PackageId}' version '{Version}' from source '{Source}'")]
    private partial void LogInstallingPlugin(string packageId, string? version, string? source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin")]
    private partial void LogError(Exception exception);
}

