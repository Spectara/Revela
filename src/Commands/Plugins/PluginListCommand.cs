using System.CommandLine;
using Spectara.Revela.Core;
using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Handles 'revela plugin list' command.
/// </summary>
public sealed partial class PluginListCommand(
    ILogger<PluginListCommand> logger)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("list", "List installed plugins");

        command.SetAction(_ =>
        {
            Execute();
            return 0;
        });

        return command;
    }

    private void Execute()
    {
        try
        {
            LogListingPlugins();
            var installedPlugins = PluginManager.ListInstalledPlugins().ToList();

            if (installedPlugins.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No plugins installed.[/]");
                AnsiConsole.MarkupLine("[dim]Install plugins with:[/] [cyan]revela plugin install <name>[/]");
                AnsiConsole.MarkupLine($"[dim]Local directory:[/] {PluginManager.LocalPluginDirectory}");
                AnsiConsole.MarkupLine($"[dim]Global directory:[/] {PluginManager.GlobalPluginDirectory}");
                return;
            }

            var table = new Table
            {
                Border = TableBorder.Rounded
            };
            table.AddColumn("[bold]Plugin[/]");
            table.AddColumn("[bold]Location[/]");

            foreach (var (name, location) in installedPlugins)
            {
                var locationStyle = location == "local" ? "[green]local[/]" : "[blue]global[/]";
                table.AddRow(name, locationStyle);
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\n[dim]Total:[/] {installedPlugins.Count} plugin(s)");
            AnsiConsole.MarkupLine($"[dim]Local directory:[/] {PluginManager.LocalPluginDirectory}");
            AnsiConsole.MarkupLine($"[dim]Global directory:[/] {PluginManager.GlobalPluginDirectory}");
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing installed plugins")]
    private partial void LogListingPlugins();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to list plugins")]
    private partial void LogError(Exception exception);
}

