using System.CommandLine;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Projects.Commands;

/// <summary>
/// Deletes a project folder in standalone mode.
/// </summary>
public sealed partial class ProjectsDeleteCommand(
    ILogger<ProjectsDeleteCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment)
{
    /// <summary>
    /// Order for menu display.
    /// </summary>
    public const int Order = 30;

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var nameArg = new Argument<string?>("name")
        {
            Description = "Name of the project folder to delete",
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("delete", "Delete a project folder");
        command.Arguments.Add(nameArg);

        command.SetAction((parseResult, _) =>
        {
            var name = parseResult.GetValue(nameArg);
            Execute(name);
            return Task.FromResult(0);
        });

        return command;
    }

    private void Execute(string? folderName)
    {
        var projectsDir = ConfigPathResolver.ProjectsDirectory;

        if (!Directory.Exists(projectsDir))
        {
            AnsiConsole.MarkupLine("[yellow]No projects directory found.[/]");
            return;
        }

        var projects = GetProjectFolders();

        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No project folders found.[/]");
            return;
        }

        // If no name provided, show selection
        if (string.IsNullOrEmpty(folderName))
        {
            var choices = projects.Select(p => p.FolderName).ToList();
            choices.Add("[dim]Cancel[/]");

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[red]Select project to delete:[/]")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Red))
                    .AddChoices(choices));

            if (selection == "[dim]Cancel[/]")
            {
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                return;
            }

            folderName = selection;
        }

        // Find the project
        var project = projects.FirstOrDefault(p =>
            p.FolderName.Equals(folderName, StringComparison.OrdinalIgnoreCase));

        if (project == default)
        {
            AnsiConsole.MarkupLine($"[red]Project folder '{folderName}' not found.[/]");
            return;
        }

        // Check if this is the current project
        var currentPath = projectEnvironment.Value.Path;
        var isCurrentProject = string.Equals(project.Path, currentPath, StringComparison.OrdinalIgnoreCase);

        if (isCurrentProject)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Warning:[/] This is the currently active project!");
            AnsiConsole.MarkupLine("[yellow]  Revela will exit after deletion.[/]");
            AnsiConsole.WriteLine();
        }

        // Show what will be deleted
        AnsiConsole.MarkupLine($"[red]This will permanently delete:[/]");
        AnsiConsole.MarkupLine($"  [cyan]{project.Path}[/]");
        AnsiConsole.WriteLine();

        // Confirmation prompt - must type project folder name
        var confirmation = AnsiConsole.Prompt(
            new TextPrompt<string>($"[red]Type '{project.FolderName}' to confirm deletion:[/]")
                .AllowEmpty());

        if (!confirmation.Equals(project.FolderName, StringComparison.Ordinal))
        {
            AnsiConsole.MarkupLine("[dim]Deletion cancelled - name did not match.[/]");
            return;
        }

        LogDeletingProject(logger, project.Path);

        try
        {
            Directory.Delete(project.Path, recursive: true);
            AnsiConsole.MarkupLine($"[green]✓[/] Deleted project folder: [cyan]{project.FolderName}[/]");

            if (isCurrentProject)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Please restart Revela.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to delete:[/] {ex.Message}");
        }
    }

    private static IReadOnlyList<(string FolderName, string Path, string DisplayName)> GetProjectFolders()
    {
        var projectsDir = ConfigPathResolver.ProjectsDirectory;

        if (!Directory.Exists(projectsDir))
        {
            return [];
        }

        var folders = new List<(string, string, string)>();

        foreach (var dir in Directory.GetDirectories(projectsDir))
        {
            var folderName = Path.GetFileName(dir);
            var projectFile = Path.Combine(dir, "project.json");

            if (File.Exists(projectFile))
            {
                var displayName = ReadProjectName(projectFile) ?? folderName;
                folders.Add((folderName, dir, displayName));
            }
            else
            {
                folders.Add((folderName, dir, folderName));
            }
        }

        return [.. folders.OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ReadProjectName(string projectFilePath)
    {
        try
        {
            var json = File.ReadAllText(projectFilePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("project", out var projectSection) &&
                projectSection.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                var name = nameElement.GetString();
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting project folder: {Path}")]
    private static partial void LogDeletingProject(ILogger logger, string path);
}
