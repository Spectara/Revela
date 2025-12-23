using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Config.Images;
using Spectara.Revela.Commands.Config.Revela;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Commands.Config.Site;
using Spectara.Revela.Commands.Config.Theme;
using Spectara.Revela.Core.Configuration;
using Spectre.Console;
using Spectara.Revela.Sdk;

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
    IOptionsMonitor<FeedsConfig> feedsConfig,
    ConfigThemeCommand themeCommand,
    ConfigSiteCommand siteCommand,
    ConfigImageCommand imageCommand,
    ConfigShowCommand showCommand,
    ConfigFeedCommand feedCommand,
    ConfigPathCommand pathCommand)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("config", "Configure project settings");

        // Project subcommands
        command.Subcommands.Add(themeCommand.Create());
        command.Subcommands.Add(siteCommand.Create());
        command.Subcommands.Add(imageCommand.Create());
        command.Subcommands.Add(showCommand.Create());

        // Revela (global) subcommands
        command.Subcommands.Add(feedCommand.Create());
        command.Subcommands.Add(pathCommand.Create());

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
                    .PageSize(14)
                    .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                    .AddChoices(
                        // Project Section
                        new MenuChoice("header-project", "───── Project Settings ─────", "", IsHeader: true),
                        new MenuChoice("theme", "Theme", "Select the site theme"),
                        new MenuChoice("site", "Site metadata", "Title, author, description"),
                        new MenuChoice("image", "Image settings", "Formats, quality, sizes"),
                        // Revela Section
                        new MenuChoice("header-revela", "───── Revela Settings ─────", "", IsHeader: true),
                        new MenuChoice("feed", "NuGet feeds", "Manage plugin sources"),
                        new MenuChoice("path", "Show paths", "Display config locations"),
                        // Actions
                        new MenuChoice("header-actions", "─────────────────────────────", "", IsHeader: true),
                        new MenuChoice("show", "Show current config", "Display configuration"),
                        new MenuChoice("exit", "Exit", "Save and exit")));

            AnsiConsole.WriteLine();

            // Skip headers (user shouldn't be able to select them, but handle it)
            if (choice.IsHeader)
            {
                continue;
            }

            if (choice.Id == "exit")
            {
                AnsiConsole.MarkupLine("[green]✓[/] Configuration complete");
                break;
            }

            var result = choice.Id switch
            {
                "theme" => await themeCommand.ExecuteAsync(null, cancellationToken).ConfigureAwait(false),
                "site" => await siteCommand.ExecuteAsync(null, null, null, null, cancellationToken).ConfigureAwait(false),
                "image" => await imageCommand.ExecuteAsync(null, null, cancellationToken).ConfigureAwait(false),
                "feed" => ExecuteFeedMenuAsync(feedsConfig.CurrentValue),
                "path" => ExecutePathCommand(),
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

    private static int ExecuteFeedMenuAsync(FeedsConfig config)
    {
        // Show current feeds
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("URL")
            .AddColumn("Type");

        table.AddRow("[cyan]nuget.org[/]", "[dim]https://api.nuget.org/v3/index.json[/]", "[blue]built-in[/]");

        foreach (var (name, url) in config.Feeds)
        {
            var feedType = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "remote" : "local";
            table.AddRow($"[cyan]{name}[/]", $"[dim]{url}[/]", feedType == "local" ? "[green]local[/]" : "[dim]remote[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Use CLI to modify:[/]");
        AnsiConsole.MarkupLine("  [cyan]revela config feed add <name> <url>[/]");
        AnsiConsole.MarkupLine("  [cyan]revela config feed remove <name>[/]");

        return 0;
    }

    private static int ExecutePathCommand()
    {
        var locationType = Core.Services.ConfigPathResolver.IsPortableInstallation ? "Portable" : "User";

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Path");

        table.AddRow("[cyan]Installation Type[/]", $"[green]{locationType}[/]");
        table.AddRow("[cyan]Config Directory[/]", $"[dim]{Core.Services.ConfigPathResolver.ConfigDirectory}[/]");
        table.AddRow("[cyan]Config File[/]", $"[dim]{Core.Services.GlobalConfigManager.ConfigFilePath}[/]");
        table.AddRow("[cyan]Plugins (local)[/]", $"[dim]{Core.Services.ConfigPathResolver.LocalPluginDirectory}[/]");

        AnsiConsole.Write(table);
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

    /// <summary>
    /// Represents a menu choice in the interactive wizard.
    /// </summary>
    private sealed record MenuChoice(string Id, string Title, string Description, bool IsHeader = false)
    {
        public override string ToString() => IsHeader
            ? $"[dim]{Title}[/]"
            : $"{Title} [dim]- {Description}[/]";
    }
}

