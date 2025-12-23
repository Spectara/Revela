using System.CommandLine;

using Spectara.Revela.Commands.Init.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Initializes all bundled project configurations (project.json, site.json).
/// </summary>
/// <remarks>
/// <para>
/// This command creates all bundled configuration files that don't exist yet.
/// Unlike individual commands, it skips files that already exist instead of
/// showing an error.
/// </para>
/// <para>
/// Usage: revela init all
/// </para>
/// </remarks>
public sealed partial class InitAllCommand(
    ILogger<InitAllCommand> logger,
    IScaffoldingService scaffoldingService,
    IThemeResolver themeResolver)
{
    /// <summary>
    /// Display order within the init command menu.
    /// </summary>
    public const int Order = 0;

    /// <summary>
    /// Creates the 'init all' command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("all", "Initialize all project configurations");

        command.SetAction(async (_, cancellationToken) => await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            AnsiConsole.MarkupLine("[blue]>[/] Initializing bundled configurations...\n");

            // Use sensible defaults
            var projectName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            var projectAuthor = Environment.UserName;
            var defaultTheme = availableThemes[0].Metadata.Name;
            var currentYear = DateTime.Now.Year;

            LogInitializingProject(projectName);

            // Template model
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

            var createdFiles = new List<string>();
            var skippedFiles = new List<string>();

            // Create project.json if it doesn't exist
            if (!File.Exists("project.json"))
            {
                var projectConfig = scaffoldingService.RenderTemplate("Project.project.json", model);
                cancellationToken.ThrowIfCancellationRequested();
                await File.WriteAllTextAsync("project.json", projectConfig, cancellationToken).ConfigureAwait(false);
                createdFiles.Add("project.json");
                AnsiConsole.MarkupLine($"[green]✓[/] Created project.json with theme [cyan]{defaultTheme}[/]");
            }
            else
            {
                skippedFiles.Add("project.json");
            }

            // Create site.json if it doesn't exist (and theme provides template)
            if (!File.Exists("site.json"))
            {
                var selectedTheme = availableThemes[0];
                await using var siteTemplateStream = selectedTheme.GetSiteTemplate();
                if (siteTemplateStream is not null)
                {
                    using var reader = new StreamReader(siteTemplateStream);
                    var siteTemplate = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    var siteConfig = scaffoldingService.RenderTemplateContent(siteTemplate, model);
                    await File.WriteAllTextAsync("site.json", siteConfig, cancellationToken).ConfigureAwait(false);
                    createdFiles.Add("site.json");
                    AnsiConsole.MarkupLine("[green]✓[/] Created site.json");
                }
            }
            else
            {
                skippedFiles.Add("site.json");
            }

            // Create directories
            Directory.CreateDirectory("source");
            Directory.CreateDirectory("output");

            // Summary
            AnsiConsole.WriteLine();

            if (createdFiles.Count > 0)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Project '{projectName}' initialized");
                AnsiConsole.MarkupLine($"[dim]Created: {string.Join(", ", createdFiles)}, source/, output/[/]");
            }

            if (skippedFiles.Count > 0)
            {
                AnsiConsole.MarkupLine($"[dim]Skipped (already exist): {string.Join(", ", skippedFiles)}[/]");
            }

            if (createdFiles.Count == 0 && skippedFiles.Count > 0)
            {
                AnsiConsole.MarkupLine("[yellow]All bundled configurations already exist.[/]");
            }

            // Show next steps
            AnsiConsole.WriteLine();
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
            ErrorPanels.ShowException(ex);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing project '{ProjectName}'")]
    private partial void LogInitializingProject(string projectName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize project")]
    private partial void LogError(Exception exception);
}

