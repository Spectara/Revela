using System.CommandLine;
using Spectara.Revela.Infrastructure.Scaffolding;
using Spectre.Console;

namespace Spectara.Revela.Features.Init;

/// <summary>
/// Handles 'revela init theme' command
/// </summary>
public static class InitThemeCommand
{
    public static Command Create()
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

    private static void Execute(string name)
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

            // Create theme directory
            Directory.CreateDirectory(themePath);

            // Copy theme templates from embedded resources
            ScaffoldingService.CopyTemplateTo("Theme.layout.html", Path.Combine(themePath, "layout.html"));
            ScaffoldingService.CopyTemplateTo("Theme.index.html", Path.Combine(themePath, "index.html"));
            ScaffoldingService.CopyTemplateTo("Theme.gallery.html", Path.Combine(themePath, "gallery.html"));

            AnsiConsole.MarkupLine($"[green]✨ Theme '{name}' created![/]");
            AnsiConsole.MarkupLine($"[dim]Theme files created in:[/] [cyan]{themePath}[/]");
            AnsiConsole.MarkupLine("[dim]Edit these files to customize your theme.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }
}

