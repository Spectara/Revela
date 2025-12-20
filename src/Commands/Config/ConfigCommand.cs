using System.CommandLine;
using Spectara.Revela.Commands.Config.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config;

/// <summary>
/// Parent command for configuration management.
/// </summary>
/// <remarks>
/// When invoked without subcommand, shows an interactive menu.
/// Plugins can register subcommands under this parent.
/// </remarks>
public sealed class ConfigCommand(
    IConfigService configService,
    ConfigThemeCommand themeCommand,
    ConfigSiteCommand siteCommand,
    ConfigImagesCommand imagesCommand,
    ConfigShowCommand showCommand)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("config", "Configure project settings");

        // Add subcommands
        command.Subcommands.Add(themeCommand.Create());
        command.Subcommands.Add(siteCommand.Create());
        command.Subcommands.Add(imagesCommand.Create());
        command.Subcommands.Add(showCommand.Create());

        // Default action: interactive menu
        command.SetAction(async (_, cancellationToken) =>
        {
            return await ExecuteInteractiveAsync(cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes the interactive configuration wizard.
    /// Called from init command after project scaffolding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code.</returns>
    public Task<int> ExecuteWizardAsync(CancellationToken cancellationToken)
        => ExecuteInteractiveAsync(cancellationToken);

    /// <summary>
    /// Executes the interactive configuration wizard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> ExecuteInteractiveAsync(CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Not a Revela project. Run [cyan]revela init project[/] first.");
            return 1;
        }

        AnsiConsole.Write(new Rule("[cyan]Revela Configuration[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<MenuChoice>()
                    .Title("[cyan]What would you like to configure?[/]")
                    .PageSize(8)
                    .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                    .AddChoices(
                        new MenuChoice("theme", "Theme", "Select the site theme"),
                        new MenuChoice("site", "Site metadata", "Title, author, description"),
                        new MenuChoice("images", "Image settings", "Formats, quality, sizes"),
                        new MenuChoice("show", "Show current config", "Display configuration"),
                        new MenuChoice("exit", "Exit", "Save and exit")));

            AnsiConsole.WriteLine();

            if (choice.Id == "exit")
            {
                AnsiConsole.MarkupLine("[green]✓[/] Configuration complete");
                break;
            }

            var result = choice.Id switch
            {
                "theme" => await themeCommand.ExecuteAsync(null, cancellationToken).ConfigureAwait(false),
                "site" => await siteCommand.ExecuteAsync(null, null, null, null, cancellationToken).ConfigureAwait(false),
                "images" => await imagesCommand.ExecuteAsync(null, null, cancellationToken).ConfigureAwait(false),
                "show" => await ExecuteShowAsync(cancellationToken).ConfigureAwait(false),
                _ => 0
            };

            if (result != 0)
            {
                // Don't exit on error, let user try again
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.WriteLine();
            }
        }

        return 0;
    }

    private async Task<int> ExecuteShowAsync(CancellationToken cancellationToken)
    {
        var projectConfig = await configService.ReadProjectConfigRawAsync(cancellationToken).ConfigureAwait(false);
        var siteConfig = await configService.ReadSiteConfigRawAsync(cancellationToken).ConfigureAwait(false);

        if (projectConfig is not null)
        {
            var json = projectConfig.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var panel = new Panel(new Text(json))
            {
                Header = new PanelHeader("[bold]project.json[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0, 1, 0)
            };
            AnsiConsole.Write(panel);
        }

        if (siteConfig is not null)
        {
            AnsiConsole.WriteLine();
            var json = siteConfig.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var panel = new Panel(new Text(json))
            {
                Header = new PanelHeader("[bold]site.json[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0, 1, 0)
            };
            AnsiConsole.Write(panel);
        }

        return 0;
    }

    /// <summary>
    /// Represents a menu choice in the interactive wizard.
    /// </summary>
    private sealed record MenuChoice(string Id, string Title, string Description)
    {
        public override string ToString() => $"{Title} [dim]- {Description}[/]";
    }
}
