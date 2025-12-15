using System.CommandLine;
using Spectara.Revela.Commands.Init.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Handles 'revela init project' command.
/// </summary>
public sealed partial class InitProjectCommand(
    ILogger<InitProjectCommand> logger,
    IScaffoldingService scaffoldingService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("project", "Initialize a new Revela project");

        // Options
        var nameOption = new Option<string?>("--name", "-n")
        {
            Description = "Project name (defaults to current directory name)"
        };

        var authorOption = new Option<string?>("--author", "-a")
        {
            Description = "Author name"
        };

        command.Options.Add(nameOption);
        command.Options.Add(authorOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameOption);
            var author = parseResult.GetValue(authorOption);

            Execute(name, author);
            return 0;
        });

        return command;
    }

    private void Execute(string? name, string? author)
    {
        try
        {
            // Check if already initialized
            if (File.Exists("project.json") || File.Exists("site.json"))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Project already initialized (project.json or site.json exists)");
                return;
            }

            AnsiConsole.MarkupLine("[blue]>[/] Initializing Revela project...");

            // Get project name (default to directory name)
            var projectName = name ?? new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            var projectAuthor = author ?? Environment.UserName;
            var currentYear = DateTime.Now.Year;

            LogInitializingProject(projectName, projectAuthor);

            // Template model
            var model = new
            {
                project = new
                {
                    name = projectName,
                    url = "https://revela.website",
                    theme = "lumina"
                },
                site = new
                {
                    title = projectName,
                    author = projectAuthor,
                    year = currentYear,
                    description = $"Photography portfolio by {projectAuthor}"
                }
            };

            // Use ScaffoldingService to render templates
            var projectConfig = scaffoldingService.RenderTemplate("Project.project.json", model);
            var siteConfig = scaffoldingService.RenderTemplate("Project.site.json", model);

            File.WriteAllText("project.json", projectConfig);
            File.WriteAllText("site.json", siteConfig);

            // Create empty directories (NO themes/)
            Directory.CreateDirectory("source");
            Directory.CreateDirectory("output");

            // Success
            var panel = new Panel($"[green]Project '{projectName}' initialized![/]\n\n" +
                                "[bold]Next steps:[/]\n" +
                                "1. Add your content to [cyan]source/[/] (photos, markdown, etc.)\n" +
                                "2. Run [cyan]revela generate[/]\n" +
                                "3. (Optional) Run [cyan]revela init theme --name custom[/] to customize theme")
            {
                Header = new PanelHeader("[bold green]Success[/]"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing project '{ProjectName}' by '{Author}'")]
    private partial void LogInitializingProject(string projectName, string author);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize project")]
    private partial void LogError(Exception exception);
}

