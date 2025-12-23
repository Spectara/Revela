using System.CommandLine;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config;

/// <summary>
/// Command to display the current configuration.
/// </summary>
/// <remarks>
/// Shows merged configuration from project.json and site.json.
/// </remarks>
public sealed class ConfigShowCommand(IConfigService configService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("show", "Display current configuration");

        var fileOption = new Option<string?>("--file", "-f")
        {
            Description = "Show only specific file (project or site)"
        };
        command.Options.Add(fileOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileOption);
            return await ExecuteAsync(file, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string? file, CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Not a Revela project. Run [cyan]revela init project[/] first.");
            return 1;
        }

        var showProject = file is null || file.Equals("project", StringComparison.OrdinalIgnoreCase);
        var showSite = file is null || file.Equals("site", StringComparison.OrdinalIgnoreCase);

        if (showProject)
        {
            var projectConfig = await configService.ReadProjectConfigRawAsync(cancellationToken).ConfigureAwait(false);
            if (projectConfig is not null)
            {
                var json = projectConfig.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var panel = new Panel(new Text(json))
                    .WithHeader("[bold]project.json[/]")
                    .WithInfoStyle();
                panel.Padding = new Padding(1, 0, 1, 0);
                AnsiConsole.Write(panel);
            }
        }

        if (showSite)
        {
            if (showProject)
            {
                AnsiConsole.WriteLine();
            }

            var siteConfig = await configService.ReadSiteConfigRawAsync(cancellationToken).ConfigureAwait(false);
            if (siteConfig is not null)
            {
                var json = siteConfig.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var panel = new Panel(new Text(json))
                    .WithHeader("[bold]site.json[/]")
                    .WithInfoStyle();
                panel.Padding = new Padding(1, 0, 1, 0);
                AnsiConsole.Write(panel);
            }
        }

        return 0;
    }
}

