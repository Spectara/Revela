using System.CommandLine;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;
using Spectre.Console;

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

        command.SetAction(async (_, cancellationToken) =>
        {
            await ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    private Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var projectPath = Environment.CurrentDirectory;
        var themes = themeResolver.GetAvailableThemes(projectPath).ToList();

        if (themes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No themes found.[/]");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Install a theme with [cyan]revela theme add <name>[/]");
            return Task.CompletedTask;
        }

        // Build content for panel
        var content = new List<string>();

        foreach (var theme in themes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = theme.Metadata;
            var source = GetThemeSource(theme);
            var sourceIcon = source == "local" ? "[blue]*[/]" : "[green]+[/]";

            content.Add($"{sourceIcon} [bold green]{EscapeMarkup(metadata.Name)}[/] [dim]v{metadata.Version}[/]");
            content.Add($"   [dim]{EscapeMarkup(metadata.Description)}[/]");

            if (source == "local")
            {
                content.Add($"   [blue]Source: themes/{EscapeMarkup(metadata.Name)}/[/]");
            }

            content.Add("");
        }

        // Remove last empty line
        if (content.Count > 0 && string.IsNullOrEmpty(content[^1]))
        {
            content.RemoveAt(content.Count - 1);
        }

        var panel = new Panel(new Markup(string.Join("\n", content)))
        {
            Header = new PanelHeader($"[bold]Available Themes[/] [dim]({themes.Count})[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[dim]Tip:[/] Use [cyan]revela theme extract <name>[/] to customize a theme");

        return Task.CompletedTask;
    }

    private static string GetThemeSource(IThemePlugin theme)
    {
        // Check if it's a local theme by type name
        var typeName = theme.GetType().Name;

        return typeName.Contains("LocalThemeAdapter", StringComparison.Ordinal)
            ? "local"
            : "installed";
    }

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }
}
