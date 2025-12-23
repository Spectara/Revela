using System.CommandLine;

using Spectara.Revela.Commands.Init.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Handles 'revela init site' command.
/// </summary>
/// <remarks>
/// Creates site.json from theme template with user-friendly defaults.
/// Requires a theme to be installed that provides a site.json template.
/// </remarks>
public sealed partial class InitSiteCommand(
    ILogger<InitSiteCommand> logger,
    IScaffoldingService scaffoldingService,
    IThemeResolver themeResolver)
{
    /// <summary>
    /// Display order within the init command menu.
    /// </summary>
    public const int Order = 20;

    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("site", "Initialize site configuration (site.json)");

        command.SetAction(async (_, cancellationToken) => await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if already exists
            if (File.Exists("site.json"))
            {
                ErrorPanels.ShowError(
                    "Already Exists",
                    "[yellow]site.json already exists.[/]\n\n" +
                    "[bold]To modify settings:[/]\n" +
                    "  Run [cyan]revela config site[/]");
                return 1;
            }

            // Check for project.json (site.json requires project context)
            if (!File.Exists("project.json"))
            {
                ErrorPanels.ShowPrerequisiteError(
                    "project.json",
                    "init project",
                    "site.json requires an initialized project.");
                return 1;
            }

            // Check for available themes
            var projectPath = Directory.GetCurrentDirectory();
            var availableThemes = themeResolver.GetAvailableThemes(projectPath).ToList();

            if (availableThemes.Count == 0)
            {
                ErrorPanels.ShowNothingInstalledError(
                    "themes",
                    "plugin install Spectara.Revela.Theme.Lumina");
                return 1;
            }

            AnsiConsole.MarkupLine("[blue]>[/] Creating site.json...");

            // Use sensible defaults
            var projectName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            var projectAuthor = Environment.UserName;
            var currentYear = DateTime.Now.Year;

            LogCreatingSiteConfig(projectName);

            // Template model
            var model = new
            {
                site = new
                {
                    title = projectName,
                    author = projectAuthor,
                    year = currentYear,
                    description = $"Photography portfolio by {projectAuthor}"
                }
            };

            // Get site.json template from theme
            var selectedTheme = availableThemes[0];
            await using var siteTemplateStream = selectedTheme.GetSiteTemplate();

            if (siteTemplateStream is null)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Theme '{selectedTheme.Metadata.Name}' doesn't provide a site.json template");
                AnsiConsole.MarkupLine("[dim]You may need to create site.json manually[/]");
                return 1;
            }

            using var reader = new StreamReader(siteTemplateStream);
            var siteTemplate = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var siteConfig = scaffoldingService.RenderTemplateContent(siteTemplate, model);

            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync("site.json", siteConfig, cancellationToken).ConfigureAwait(false);

            // Success message
            AnsiConsole.MarkupLine($"\n[green]✓[/] Created site.json");
            AnsiConsole.MarkupLine("[dim]Customize with 'revela config site'[/]");

            return 0;
        }
        catch (Exception ex)
        {
            LogError(ex);
            ErrorPanels.ShowException(ex);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating site.json for project '{ProjectName}'")]
    private partial void LogCreatingSiteConfig(string projectName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create site.json")]
    private partial void LogError(Exception exception);
}
