using System.CommandLine;
using Spectara.Revela.Commands.Config.Project;
using Spectara.Revela.Commands.Config.Revela;
using Spectara.Revela.Commands.Config.Site;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config;

/// <summary>
/// Parent command for configuration management.
/// </summary>
/// <remarks>
/// Only registers host-owned subcommands (project, site, locations).
/// Feed commands are registered by <c>PackagesCommandProvider</c> (only in Cli).
/// Plugin config commands (theme, image, sorting, paths) are registered by
/// their respective plugins via <c>ParentCommand: "config"</c>.
/// </remarks>
internal sealed class ConfigCommand(
    IConfigService configService,
    ConfigProjectCommand projectCommand,
    ConfigSiteCommand siteCommand,
    ConfigLocationsCommand locationsCommand)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("config", "Configure project settings");

        // Host-owned subcommands only
        // Feed subcommands are added by PackagesCommandProvider via ParentCommand: "config"
        // Plugin subcommands (theme, image, sorting, paths) are added
        // automatically by the host via ParentCommand: "config"
        command.Subcommands.Add(projectCommand.Create());
        command.Subcommands.Add(siteCommand.Create());
        command.Subcommands.Add(locationsCommand.Create());

        // Default action: show current config (interactive menu removed — use CLI subcommands)
        command.SetAction(async (_, cancellationToken) => await ExecuteShowAsync(cancellationToken));

        return command;
    }

    private async Task<int> ExecuteShowAsync(CancellationToken cancellationToken)
    {
        var projectConfig = await configService.ReadProjectConfigAsync(cancellationToken);
        var siteConfig = await configService.ReadSiteConfigAsync(cancellationToken);

        if (projectConfig is not null)
        {
            var json = projectConfig.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var panel = new Panel(new Text(json))
                .WithHeader("[bold]project.json[/]")
                .WithInfoStyle();
            panel.Padding = new Padding(1, 0, 1, 0);
            AnsiConsole.Write(panel);
        }

        if (siteConfig is not null)
        {
            AnsiConsole.WriteLine();
            var json = siteConfig.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var panel = new Panel(new Text(json))
                .WithHeader("[bold]site.json[/]")
                .WithInfoStyle();
            panel.Padding = new Padding(1, 0, 1, 0);
            AnsiConsole.Write(panel);
        }

        return 0;
    }
}

