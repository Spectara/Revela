using System.CommandLine;
using Spectara.Revela.Features.Init.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Features.Init;

/// <summary>
/// Handles 'revela init theme' command.
/// </summary>
public sealed partial class InitThemeCommand(
    ILogger<InitThemeCommand> logger,
    IScaffoldingService scaffoldingService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("theme", "Initialize a new theme");

        var nameOption = new Option<string>("--name", "-n")
        {
            Description = "Theme name",
            Required = true
        };

        command.Options.Add(nameOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameOption);
            Execute(name!);
            return 0;
        });

        return command;
    }

    private void Execute(string name)
    {
        try
        {
            if (!File.Exists("project.json"))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Not in a Revela project (project.json not found)");
                return;
            }

            var themePath = Path.Combine("themes", name);
            if (Directory.Exists(themePath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Theme '{name}' already exists");
                return;
            }

            AnsiConsole.MarkupLine($"[blue]🎨 Creating theme '{name}'...[/]");
            LogCreatingTheme(name);

            // Create theme directory
            Directory.CreateDirectory(themePath);

            // Copy theme templates from embedded resources
            scaffoldingService.CopyTemplateTo("Theme.layout.html", Path.Combine(themePath, "layout.html"));
            scaffoldingService.CopyTemplateTo("Theme.index.html", Path.Combine(themePath, "index.html"));
            scaffoldingService.CopyTemplateTo("Theme.gallery.html", Path.Combine(themePath, "gallery.html"));

            AnsiConsole.MarkupLine($"[green]✨ Theme '{name}' created![/]");
            AnsiConsole.MarkupLine($"[dim]Theme files created in:[/] [cyan]{themePath}[/]");
            AnsiConsole.MarkupLine("[dim]Edit these files to customize your theme.[/]");
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating theme '{ThemeName}'")]
    private partial void LogCreatingTheme(string themeName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create theme")]
    private partial void LogError(Exception exception);
}

