using System.CommandLine;
using Spectara.Revela.Commands.Config.Models;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Commands.Init.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Project;

/// <summary>
/// Command to configure project settings.
/// </summary>
/// <remarks>
/// Creates project.json if it doesn't exist, otherwise updates.
/// Configures name, url, theme in project.json.
/// </remarks>
public sealed partial class ConfigProjectCommand(
    ILogger<ConfigProjectCommand> logger,
    IConfigService configService,
    IScaffoldingService scaffoldingService,
    IThemeResolver themeResolver)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("project", "Configure project settings");

        var nameOption = new Option<string?>("--name", "-n")
        {
            Description = "Project name"
        };
        var urlOption = new Option<string?>("--url", "-u")
        {
            Description = "Base URL for the generated site"
        };
        var themeOption = new Option<string?>("--theme", "-t")
        {
            Description = "Theme to use"
        };

        command.Options.Add(nameOption);
        command.Options.Add(urlOption);
        command.Options.Add(themeOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameOption);
            var url = parseResult.GetValue(urlOption);
            var theme = parseResult.GetValue(themeOption);

            return await ExecuteAsync(name, url, theme, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(
        string? nameArg,
        string? urlArg,
        string? themeArg,
        CancellationToken cancellationToken)
    {
        var isFirstTime = !configService.IsProjectInitialized();

        // Check for available themes
        var projectPath = Directory.GetCurrentDirectory();
        var availableThemes = themeResolver.GetAvailableThemes(projectPath).ToList();

        if (availableThemes.Count == 0)
        {
            ErrorPanels.ShowNothingInstalledError(
                "themes",
                "plugin install Spectara.Revela.Theme.Lumina",
                "theme list --online");
            return 1;
        }

        // Get current config (or defaults for new project)
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false)
            ?? new ProjectConfigDto();

        // Determine if interactive mode (no arguments provided)
        var isInteractive = nameArg is null && urlArg is null && themeArg is null;

        string name, url, theme;

        if (isInteractive)
        {
            var action = isFirstTime ? "Initialize" : "Configure";
            AnsiConsole.MarkupLine($"[cyan]{action} project settings[/]\n");

            // Default name from current directory
            var defaultName = current.Name ?? new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            name = AnsiConsole.Prompt(
                new TextPrompt<string>("Project name:")
                    .DefaultValue(defaultName));

            url = AnsiConsole.Prompt(
                new TextPrompt<string>("Base URL:")
                    .DefaultValue(current.Url ?? "https://example.com")
                    .AllowEmpty());

            // Theme selection
            var currentTheme = current.Theme ?? availableThemes[0].Metadata.Name;
            if (availableThemes.Count == 1)
            {
                theme = availableThemes[0].Metadata.Name;
                AnsiConsole.MarkupLine($"Theme: [cyan]{theme}[/] [dim](only available)[/]");
            }
            else
            {
                theme = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select theme:")
                        .AddChoices(availableThemes.Select(t => t.Metadata.Name))
                        .UseConverter(t => t == currentTheme ? $"{t} [dim](current)[/]" : t));
            }
        }
        else
        {
            // Use provided arguments, fall back to current values or defaults
            name = nameArg ?? current.Name ?? new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            url = urlArg ?? current.Url ?? "";
            theme = themeArg ?? current.Theme ?? availableThemes[0].Metadata.Name;
        }

        // Validate theme exists
        if (!availableThemes.Any(t => t.Metadata.Name.Equals(theme, StringComparison.OrdinalIgnoreCase)))
        {
            ErrorPanels.ShowError(
                "Theme Not Found",
                $"[yellow]Theme '{theme}' is not installed.[/]\n\n" +
                "[bold]Available themes:[/]\n" +
                string.Join("\n", availableThemes.Select(t => $"  • {t.Metadata.Name}")));
            return 1;
        }

        if (isFirstTime)
        {
            // Create new project using template
            LogInitializingProject(name);

            var model = new
            {
                project = new
                {
                    name,
                    url,
                    theme
                },
                site = new
                {
                    title = name,
                    author = Environment.UserName,
                    year = DateTime.Now.Year,
                    description = $"Photography portfolio by {Environment.UserName}"
                }
            };

            var projectConfig = scaffoldingService.RenderTemplate("Project.project.json", model);
            await File.WriteAllTextAsync(configService.ProjectConfigPath, projectConfig, cancellationToken).ConfigureAwait(false);

            // Create directories
            Directory.CreateDirectory("source");
            Directory.CreateDirectory("output");

            AnsiConsole.MarkupLine($"\n[green]✓[/] Project '{name}' initialized with theme [cyan]{theme}[/]");
            AnsiConsole.MarkupLine("[dim]Created: project.json, source/, output/[/]");
        }
        else
        {
            // Update existing project
            var update = new ProjectConfigDto
            {
                Name = name,
                Url = url,
                Theme = theme
            };

            await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

            LogProjectConfigured(name, theme);
            AnsiConsole.MarkupLine("[green]✓[/] Project settings updated");
        }

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing project '{ProjectName}'")]
    private partial void LogInitializingProject(string projectName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project configured: name='{Name}', theme='{Theme}'")]
    private partial void LogProjectConfigured(string name, string theme);
}
