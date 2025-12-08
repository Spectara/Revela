using System.CommandLine;

using Spectre.Console;

using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Command to list available themes.
/// </summary>
/// <remarks>
/// Shows themes from three sources:
/// 1. Local themes (project/themes/ folder)
/// 2. Installed theme plugins
/// 3. (Future) Available themes from NuGet
/// </remarks>
public sealed partial class ThemeListCommand(IThemeResolver themeResolver)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    /// <returns>The configured list command.</returns>
    public Command Create()
    {
        var command = new Command("list", "List available themes");

        command.SetAction(_ =>
        {
            Execute();
            return 0;
        });

        return command;
    }

    private void Execute()
    {
        var projectPath = Environment.CurrentDirectory;
        var themes = themeResolver.GetAvailableThemes(projectPath).ToList();

        if (themes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No themes found.[/]");
            AnsiConsole.MarkupLine("Install a theme with [blue]revela theme add <name>[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Version");
        table.AddColumn("Source");
        table.AddColumn("Description");

        foreach (var theme in themes)
        {
            var metadata = theme.Metadata;
            var source = GetThemeSource(theme);

            table.AddRow(
                $"[green]{EscapeMarkup(metadata.Name)}[/]",
                metadata.Version,
                source,
                EscapeMarkup(metadata.Description));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[dim]Found {themes.Count} theme(s)[/]");
    }

    private static string GetThemeSource(IThemePlugin theme)
    {
        // Check if it's a local theme by type name
        var typeName = theme.GetType().Name;

        if (typeName.Contains("LocalThemeAdapter", StringComparison.Ordinal))
        {
            return "[blue]local[/]";
        }

        return "[dim]installed[/]";
    }

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }
}
