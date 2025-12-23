using System.CommandLine;
using Spectara.Revela.Commands.Init.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Handles 'revela init project' command.
/// </summary>
public sealed partial class InitProjectCommand(
    ILogger<InitProjectCommand> logger,
    IScaffoldingService scaffoldingService,
    IThemeResolver themeResolver)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("project", "Initialize project.json and folders");

        command.SetAction(async (_, cancellationToken) => await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if already initialized
            if (File.Exists("project.json") || File.Exists("site.json"))
            {
                ErrorPanels.ShowError(
                    "Already Initialized",
                    "[yellow]Project already initialized.[/]\n\n" +
                    "[dim]project.json or site.json already exists in this directory.[/]\n\n" +
                    "[bold]To modify settings:[/]\n" +
                    "  Run [cyan]revela config[/]");
                return 1;
            }

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

            AnsiConsole.MarkupLine("[blue]>[/] Initializing Revela project...");

            // Use sensible defaults - user can customize via 'revela config'
            var projectName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            var projectAuthor = Environment.UserName;
            var defaultTheme = availableThemes[0].Metadata.Name;
            var currentYear = DateTime.Now.Year;

            LogInitializingProject(projectName);

            // Template model - use first available theme as default
            var model = new
            {
                project = new
                {
                    name = projectName,
                    url = "https://revela.website",
                    theme = defaultTheme
                },
                site = new
                {
                    title = projectName,
                    author = projectAuthor,
                    year = currentYear,
                    description = $"Photography portfolio by {projectAuthor}"
                }
            };

            // Use ScaffoldingService to render project.json template
            var projectConfig = scaffoldingService.RenderTemplate("Project.project.json", model);

            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync("project.json", projectConfig, cancellationToken).ConfigureAwait(false);

            // Create empty directories
            Directory.CreateDirectory("source");
            Directory.CreateDirectory("output");

            // Success message
            AnsiConsole.MarkupLine($"\n[green]✓[/] Project '{projectName}' initialized with theme [cyan]{defaultTheme}[/]");
            AnsiConsole.MarkupLine("[dim]Created: project.json, source/, output/[/]\n");
            AnsiConsole.MarkupLine("[dim]Run 'revela init site' to create site.json[/]\n");

            // Show next steps
            var panel = new Panel("[bold]Next steps:[/]\n" +
                                "1. Run [cyan]revela config[/] to customize settings\n" +
                                "2. Add photos to [cyan]source/[/]\n" +
                                "3. Run [cyan]revela generate[/]")
                .WithInfoStyle();
            AnsiConsole.Write(panel);

            return 0;
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing project '{ProjectName}'")]
    private partial void LogInitializingProject(string projectName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize project")]
    private partial void LogError(Exception exception);
}
