using System.CommandLine;
using Spectara.Revela.Core;
using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Handles 'revela plugin list' command.
/// </summary>
public sealed partial class PluginListCommand(
    ILogger<PluginListCommand> logger,
    PluginManager pluginManager)
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
            LogError(ex);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing installed plugins")]
    private partial void LogListingPlugins();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to list plugins")]
    private partial void LogError(Exception exception);
}

