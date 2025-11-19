using System.CommandLine;
using Spectara.Revela.Core;
using Spectre.Console;

namespace Spectara.Revela.Features.Plugins;

/// <summary>
/// Handles 'revela plugin uninstall' command
/// </summary>
public static class PluginUninstallCommand
{
    public static Command Create()
    {
        var command = new Command("uninstall", "Uninstall a plugin");

        var nameArgument = new Argument<string>("name")
        {
            Description = "Plugin name to uninstall"
        };
        command.Arguments.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument);
            return ExecuteAsync(name!).GetAwaiter().GetResult();
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(string name)
    {
        try
        {
            // Convert short name to full package ID
            var packageId = name.StartsWith("Revela.Plugin.", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"Revela.Plugin.{name}";

            if (!await AnsiConsole.ConfirmAsync($"[yellow]Uninstall plugin '{packageId}'?[/]"))
            {
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[blue]🗑️  Uninstalling plugin:[/] [cyan]{packageId}[/]");

            var pluginManager = new PluginManager();
            var success = await pluginManager.UninstallPluginAsync(packageId);

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✨ Plugin '{packageId}' uninstalled successfully![/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Plugin '{packageId}' not found.[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

