using System.CommandLine;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Revela;

/// <summary>
/// Command to display configuration and plugin locations.
/// </summary>
public sealed partial class ConfigLocationsCommand(
    ILogger<ConfigLocationsCommand> logger)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("locations", "Display configuration and plugin locations");

        command.SetAction((_, _) =>
        {
            LogDisplayingLocations(logger);
            var locationType = ConfigPathResolver.IsPortableInstallation ? "Portable" : "User";

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Setting")
                .AddColumn("Path");

            table.AddRow("[cyan]Installation Type[/]", $"[green]{locationType}[/]");
            table.AddRow("[cyan]Config Directory[/]", $"[dim]{ConfigPathResolver.ConfigDirectory}[/]");
            table.AddRow("[cyan]Config File[/]", $"[dim]{GlobalConfigManager.ConfigFilePath}[/]");
            table.AddRow("[cyan]Plugins (local)[/]", $"[dim]{ConfigPathResolver.LocalPluginDirectory}[/]");
            table.AddRow("[cyan]Plugins (global)[/]", $"[dim]{ConfigPathResolver.GlobalPluginDirectory}[/]");

            AnsiConsole.Write(table);

            // Show if config exists
            AnsiConsole.WriteLine();
            if (File.Exists(GlobalConfigManager.ConfigFilePath))
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} Configuration file exists");
            }
            else
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Configuration file not found");
                AnsiConsole.MarkupLine("  Run [cyan]revela init revela[/] to create it");
            }

            return Task.FromResult(0);
        });

        return command;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Displaying configuration locations")]
    private static partial void LogDisplayingLocations(ILogger logger);
}
