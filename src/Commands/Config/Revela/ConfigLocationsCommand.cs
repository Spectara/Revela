using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Revela;

/// <summary>
/// Command to display configuration and plugin locations.
/// </summary>
internal sealed partial class ConfigLocationsCommand(
    ILogger<ConfigLocationsCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment)
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
            table.AddRow("[cyan]Config Directory[/]", $"[dim]{Markup.Escape(ConfigPathResolver.ConfigDirectory)}[/]");
            table.AddRow("[cyan]Config File[/]", $"[dim]{Markup.Escape(ConfigPathResolver.ConfigFilePath)}[/]");
            table.AddRow("[cyan]Plugins (local)[/]", $"[dim]{Markup.Escape(ConfigPathResolver.LocalPluginDirectory)}[/]");
            table.AddRow("[cyan]Plugins (global)[/]", $"[dim]{Markup.Escape(ConfigPathResolver.GlobalPluginDirectory)}[/]");

            var environment = projectEnvironment.Value;
            if (environment.IsInitialized)
            {
                table.AddRow("[cyan]Project Config[/]", $"[dim]{Markup.Escape(Path.Combine(environment.Path, "project.json"))}[/]");
                table.AddRow("[cyan]Site Config[/]", $"[dim]{Markup.Escape(Path.Combine(environment.Path, "site.json"))}[/]");
                table.AddRow("[cyan]Logging Config[/]", $"[dim]{Markup.Escape(Path.Combine(environment.Path, "logging.json"))}[/]");
            }

            AnsiConsole.Write(table);

            // Show if config exists
            AnsiConsole.WriteLine();
            if (File.Exists(ConfigPathResolver.ConfigFilePath))
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
