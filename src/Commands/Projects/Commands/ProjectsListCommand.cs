using System.CommandLine;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Projects.Commands;

/// <summary>
/// Lists all project folders in standalone mode.
/// </summary>
public sealed partial class ProjectsListCommand(
    ILogger<ProjectsListCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment)
{
    /// <summary>
    /// Order for menu display.
    /// </summary>
    public const int Order = 10;

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("list", "List all project folders");

        command.SetAction((_, _) =>
        {
            Execute();
            return Task.FromResult(0);
        });

        return command;
    }

    private void Execute()
    {
        LogListingProjects(logger);

        var projectsDir = ConfigPathResolver.ProjectsDirectory;
        var currentPath = projectEnvironment.Value.Path;

        if (!Directory.Exists(projectsDir))
        {
            AnsiConsole.MarkupLine("[yellow]No projects directory found.[/]");
            return;
        }

        var projects = GetProjectFolders();

        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No project folders found.[/]");
            AnsiConsole.MarkupLine($"[dim]Projects directory: {projectsDir}[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(new Style(Color.Grey))
            .AddColumn(new TableColumn("[cyan]Name[/]").LeftAligned())
            .AddColumn(new TableColumn("[cyan]Folder[/]").LeftAligned())
            .AddColumn(new TableColumn("[cyan]Status[/]").Centered());

        foreach (var (folderName, path, displayName) in projects)
        {
            var isCurrent = string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase);
            var projectFile = Path.Combine(path, "project.json");
            var isConfigured = File.Exists(projectFile);

            var nameMarkup = isCurrent
                ? $"[bold green]{EscapeMarkup(displayName)}[/]"
                : EscapeMarkup(displayName);

            var folderMarkup = $"[dim]{EscapeMarkup(folderName)}[/]";

            var statusMarkup = isCurrent
                ? "[green]● active[/]"
                : isConfigured
                    ? "[dim]configured[/]"
                    : "[yellow]not configured[/]";

            table.AddRow(nameMarkup, folderMarkup, statusMarkup);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Projects directory: {projectsDir}[/]");
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

    private static string EscapeMarkup(string text) =>
        text.Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing projects")]
    private static partial void LogListingProjects(ILogger logger);
}
