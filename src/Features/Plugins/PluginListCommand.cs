using System.CommandLine;
using Spectara.Revela.Core;
using Spectre.Console;

namespace Spectara.Revela.Features.Plugins;

/// <summary>
/// Handles 'revela plugin list' command
/// </summary>
public static class PluginListCommand
{
    public static Command Create()
    {
        var command = new Command("list", "List installed plugins");

        command.SetAction(_ =>
        {
            Execute();
            return 0;
        });

        return command;
    }

    private static void Execute()
    {
        try
        {
            var pluginManager = new PluginManager();
            var installedPlugins = pluginManager.ListInstalledPlugins().ToList();

            if (installedPlugins.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No plugins installed.[/]");
                AnsiConsole.MarkupLine("[dim]Install plugins with:[/] [cyan]revela plugin install <name>[/]");
                return;
            }

            var table = new Table
            {
                Border = TableBorder.Rounded
            };
            table.AddColumn("[bold]Plugin[/]");

            foreach (var plugin in installedPlugins)
            {
                table.AddRow(plugin);
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\n[dim]Total:[/] {installedPlugins.Count} plugin(s)");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }
}

