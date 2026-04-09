using System.CommandLine;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;
using Spectre.Console;

namespace Spectara.Revela.Features.Theme.Commands;

/// <summary>
/// Command to configure the theme — thin UI wrapper around <see cref="IThemeService"/>.
/// </summary>
internal sealed partial class ConfigThemeCommand(
    ILogger<ConfigThemeCommand> logger,
    IConfigService configService,
    IThemeService themeService)
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
            return await ExecuteAsync(theme, cancellationToken);
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
            ErrorPanels.ShowNotAProjectError();
            return 1;
        }

        var current = themeService.GetCurrentTheme();
        var listResult = await themeService.ListAsync(cancellationToken: cancellationToken);

        if (listResult.Installed.Count == 0)
        {
            ErrorPanels.ShowNothingInstalledError(
                "themes",
                "theme install Spectara.Revela.Themes.Lumina",
                "theme list --online");
            return 1;
        }

        string selectedTheme;

        if (!string.IsNullOrEmpty(themeArg))
        {
            var match = listResult.Installed.FirstOrDefault(
                t => t.Metadata.Name.Equals(themeArg, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                var availableList = string.Join("\n", listResult.Installed.Select(
                    t => $"  [green]{t.Metadata.Name}[/] [dim]({(t.IsLocal ? "local" : t.Source?.ToString() ?? "installed")})[/]"));
                ErrorPanels.ShowError(
                    "Theme Not Found",
                    $"[yellow]Theme '{themeArg}' not found.[/]\n\n" +
                    $"[bold]Available themes:[/]\n{availableList}");
                return 1;
            }

            selectedTheme = match.Metadata.Name;
        }
        else
        {
            var choices = listResult.Installed
                .Select(t => new ThemeChoice(
                    t.Metadata.Name,
                    t.IsLocal ? "local" : (t.Source?.ToString() ?? "installed"),
                    t.Metadata.Name.Equals(current.ThemeName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<ThemeChoice>()
                    .Title($"[cyan]Select theme[/] [dim](current: {current.ThemeName})[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                    .AddChoices(choices));

            selectedTheme = selection.Name;
        }

        if (selectedTheme.Equals(current.ThemeName, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[dim]Theme unchanged:[/] [green]{selectedTheme}[/]");
            return 0;
        }

        await themeService.SetActiveThemeAsync(selectedTheme, cancellationToken);

        LogThemeChanged(current.ThemeName, selectedTheme);
        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Theme set to: [bold]{selectedTheme}[/]");
        return 0;
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








