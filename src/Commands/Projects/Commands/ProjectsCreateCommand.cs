using System.CommandLine;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Commands.Projects.Commands;

/// <summary>
/// Creates a new project folder in standalone mode.
/// </summary>
public sealed partial class ProjectsCreateCommand(
    ILogger<ProjectsCreateCommand> logger)
{
    /// <summary>
    /// Order for menu display.
    /// </summary>
    public const int Order = 20;

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("create", "Create a new project folder");

        command.SetAction((_, _) =>
        {
            Execute();
            return Task.FromResult(0);
        });

        return command;
    }

    private void Execute()
    {
        LogCreatingProject(logger);

        var newPath = CreateNewProjectFolder();

        if (newPath is null)
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"{OutputMarkers.Info} Restart Revela to use the new project.");
    }

    private static string? CreateNewProjectFolder()
    {
        AnsiConsole.WriteLine();

        var folderName = AnsiConsole.Prompt(
            new TextPrompt<string>("[blue]Folder name:[/]")
                .Validate(name =>
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        return ValidationResult.Error("[red]Name cannot be empty[/]");
                    }

                    var invalidChars = Path.GetInvalidFileNameChars();
                    if (name.Any(c => invalidChars.Contains(c)))
                    {
                        return ValidationResult.Error("[red]Name contains invalid characters[/]");
                    }

                    var targetPath = Path.Combine(ConfigPathResolver.ProjectsDirectory, name);
                    if (Directory.Exists(targetPath))
                    {
                        return ValidationResult.Error($"[red]Folder '{name}' already exists[/]");
                    }

                    return ValidationResult.Success();
                }));

        var projectPath = Path.Combine(ConfigPathResolver.ProjectsDirectory, folderName);

        Directory.CreateDirectory(projectPath);

        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Project folder created: [cyan]{folderName}[/]");

        return projectPath;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating new project folder")]
    private static partial void LogCreatingProject(ILogger logger);
}
