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
        var command = new Command("install", "Install a plugin from NuGet or ZIP file");

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

        var fromZipOption = new Option<string?>("--from-zip", "-z")
        {
            Description = "Install from a ZIP file (local path or URL)"
        };
        command.Options.Add(fromZipOption);

        var globalOption = new Option<bool>("--global", "-g")
        {
            Description = "Install globally to AppData (default: local, next to executable)"
        };
        command.Options.Add(globalOption);

        command.SetAction(async parseResult =>
        {
            var name = parseResult.GetValue(nameArgument);
            var version = parseResult.GetValue(versionOption);
            var fromZip = parseResult.GetValue(fromZipOption);
            var global = parseResult.GetValue(globalOption);

            if (!string.IsNullOrEmpty(fromZip))
            {
                return await ExecuteFromZipAsync(fromZip, global);
            }
            else if (!string.IsNullOrEmpty(name))
            {
                return await ExecuteFromNuGetAsync(name, version, global);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Either provide a plugin name or use --from-zip");
                return 1;
            }
        });

        return command;
    }

    private async Task<int> ExecuteFromNuGetAsync(string name, string? version, bool global)
    {
        try
        {
            // Convert short name to full package ID
            var packageId = name.StartsWith("Revela.Plugin.", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"Revela.Plugin.{name}";

            var location = global ? "globally" : "locally";
            AnsiConsole.MarkupLine($"[blue]Installing plugin {location}:[/] [cyan]{packageId}[/]");
            LogInstallingPlugin(packageId, version);

            var success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing...", async ctx =>
                {
                    ctx.Status($"Downloading {packageId}...");
                    return await pluginManager.InstallPluginAsync(packageId, version, global);
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

    private async Task<int> ExecuteFromZipAsync(string zipPath, bool global)
    {
        try
        {
            var location = global ? "globally" : "locally";
            var targetDir = global ? PluginManager.GlobalPluginDirectory : PluginManager.LocalPluginDirectory;

            AnsiConsole.MarkupLine($"[blue]Installing plugin {location} from ZIP:[/] [cyan]{zipPath}[/]");
            AnsiConsole.MarkupLine($"[dim]Target: {targetDir}[/]");
            LogInstallingFromZip(zipPath);

            var success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing...", async ctx =>
                {
                    if (zipPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Status("Downloading ZIP...");
                    }
                    else
                    {
                        ctx.Status("Extracting ZIP...");
                    }
                    return await pluginManager.InstallFromZipAsync(zipPath, global);
                });

            if (success)
            {
                AnsiConsole.MarkupLine("[green]Plugin installed successfully.[/]");
                AnsiConsole.MarkupLine("[dim]The plugin will be available after restarting revela.[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to install plugin from ZIP[/]");
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin '{PackageId}' version '{Version}'")]
    private partial void LogInstallingPlugin(string packageId, string? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from ZIP: {ZipPath}")]
    private partial void LogInstallingFromZip(string zipPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin")]
    private partial void LogError(Exception exception);
}

