using System.CommandLine;
using Spectara.Revela.Commands.Config.Models;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config;

/// <summary>
/// Command to configure the theme.
/// </summary>
/// <remarks>
/// Shows available themes and allows selection.
/// Validates that selected theme exists.
/// </remarks>
public sealed partial class ConfigThemeCommand(
    ILogger<ConfigThemeCommand> logger,
    IConfigService configService,
    IThemeResolver themeResolver)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("theme", "Configure the theme");

        var themeOption = new Option<string?>("--set", "-s")
        {
            Description = "Set theme directly (non-interactive)"
        };
        command.Options.Add(themeOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var theme = parseResult.GetValue(themeOption);
            return await ExecuteAsync(theme, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes the theme configuration.
    /// </summary>
    /// <param name="themeArg">Optional theme name to set directly.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> ExecuteAsync(string? themeArg, CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Not a Revela project. Run [cyan]revela init project[/] first.");
            return 1;
        }

        // Get current config
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);
        var currentTheme = current?.Theme;

        // Get available themes
        var projectPath = Directory.GetCurrentDirectory();
        var availableThemes = themeResolver.GetAvailableThemes(projectPath).ToList();

        if (availableThemes.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No themes available.\n");
            AnsiConsole.MarkupLine("Install a theme first:");
            AnsiConsole.MarkupLine("  [cyan]revela plugin install Spectara.Revela.Theme.Lumina[/]");
            return 1;
        }

        string selectedTheme;

        if (!string.IsNullOrEmpty(themeArg))
        {
            // Validate provided theme
            var matchingTheme = availableThemes.FirstOrDefault(
                t => t.Metadata.Name.Equals(themeArg, StringComparison.OrdinalIgnoreCase));

            if (matchingTheme is null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Theme '{themeArg}' not found.\n");
                AnsiConsole.MarkupLine("Available themes:");
                foreach (var t in availableThemes)
                {
                    var source = GetThemeSource(t);
                    AnsiConsole.MarkupLine($"  [green]{t.Metadata.Name}[/] [dim]({source})[/]");
                }
                return 1;
            }

            selectedTheme = matchingTheme.Metadata.Name;
        }
        else
        {
            // Interactive selection
            var choices = availableThemes
                .Select(t => new ThemeChoice(t.Metadata.Name, GetThemeSource(t), t.Metadata.Name.Equals(currentTheme, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<ThemeChoice>()
                    .Title($"[cyan]Select theme[/] [dim](current: {currentTheme ?? "none"})[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                    .AddChoices(choices));

            selectedTheme = selection.Name;
        }

        // Check if theme changed
        if (selectedTheme.Equals(currentTheme, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[dim]Theme unchanged:[/] [green]{selectedTheme}[/]");
            return 0;
        }

        // Save theme using DTO
        var update = new ProjectConfigDto { Theme = selectedTheme };
        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        LogThemeChanged(currentTheme ?? "none", selectedTheme);
        AnsiConsole.MarkupLine($"[green]✓[/] Theme set to: [bold]{selectedTheme}[/]");

        return 0;
    }

    private static string GetThemeSource(IThemePlugin theme)
    {
        var typeName = theme.GetType().Name;
        return typeName.Contains("LocalThemeAdapter", StringComparison.Ordinal) ? "local" : "built-in";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Theme changed from '{OldTheme}' to '{NewTheme}'")]
    private partial void LogThemeChanged(string oldTheme, string newTheme);

    /// <summary>
    /// Represents a theme choice in the selection prompt.
    /// </summary>
    private sealed record ThemeChoice(string Name, string Source, bool IsCurrent)
    {
        public override string ToString() => IsCurrent
            ? $"{Name} [dim]({Source})[/] [green]← current[/]"
            : $"{Name} [dim]({Source})[/]";
    }
}
