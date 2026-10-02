using System.CommandLine;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Models;
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
    IThemeService themeService,
    IConsoleCapabilities consoleCapabilities)
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
        var viewerOption = new Option<string?>("--viewer")
        {
            Description = "Set the project photo viewer mode (page, lightbox, or none)"
        };
        var clearViewerOption = new Option<bool>("--clear-viewer")
        {
            Description = "Use the selected theme's default photo viewer mode"
        };
        command.Options.Add(themeOption);
        command.Options.Add(viewerOption);
        command.Options.Add(clearViewerOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var theme = parseResult.GetValue(themeOption);
            var viewer = parseResult.GetValue(viewerOption);
            var clearViewer = parseResult.GetValue(clearViewerOption);
            return await ExecuteAsync(theme, viewer, clearViewer, cancellationToken);
        });

        return command;
    }

    /// <summary>
    /// Executes the theme configuration.
    /// </summary>
    /// <param name="themeArg">Optional theme name to set directly.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code.</returns>
    public Task<int> ExecuteAsync(string? themeArg, CancellationToken cancellationToken) =>
        ExecuteAsync(themeArg, null, clearViewer: false, cancellationToken);

    /// <summary>
    /// Executes the theme and viewer configuration.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? themeArg,
        string? viewerArg,
        bool clearViewer,
        CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            ErrorPanels.ShowNotAProjectError();
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(viewerArg) && clearViewer)
        {
            ErrorPanels.ShowError("Invalid Options", "[yellow]--viewer and --clear-viewer are mutually exclusive.[/]");
            return 1;
        }

        PhotoViewerMode? viewer = null;
        if (!string.IsNullOrWhiteSpace(viewerArg) && !TryParseViewer(viewerArg, out viewer))
        {
            ErrorPanels.ShowError(
                "Invalid Photo Viewer",
                $"[yellow]Unknown viewer mode '{Markup.Escape(viewerArg)}'.[/] Supported values: page, lightbox, none.");
            return 1;
        }

        var promptForTheme = string.IsNullOrEmpty(themeArg) && viewer is null && !clearViewer;
        if (promptForTheme && !consoleCapabilities.EnsureInteractive(
            InteractiveInput.NoOptionsGiven,
            "revela config theme --set <theme> [--viewer page|lightbox|none]",
            "revela config theme --viewer page|lightbox|none",
            "revela config theme --clear-viewer"))
        {
            return 1;
        }

        var current = themeService.GetCurrentTheme();
        var currentThemeName = string.IsNullOrWhiteSpace(current.ThemeName) ? Sdk.Configuration.ThemeConfig.DefaultName : current.ThemeName;
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
                    t => $"  [green]{Markup.Escape(t.Metadata.Name)}[/] " +
                        $"[dim]({Markup.Escape(t.IsLocal ? "local" : t.Source?.ToString() ?? "installed")})[/]"));
                ErrorPanels.ShowError(
                    "Theme Not Found",
                    $"[yellow]Theme '{Markup.Escape(themeArg)}' not found.[/]\n\n" +
                    $"[bold]Available themes:[/]\n{availableList}");
                return 1;
            }

            selectedTheme = match.Metadata.Name;
        }
        else if (promptForTheme)
        {
            var choices = listResult.Installed
                .Select(t => new ThemeChoice(
                    t.Metadata.Name,
                    t.IsLocal ? "local" : (t.Source?.ToString() ?? "installed"),
                    t.Metadata.Name.Equals(currentThemeName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<ThemeChoice>()
                    .Title($"[cyan]Select theme[/] [dim](current: {Markup.Escape(currentThemeName)})[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                    .AddChoices(choices));

            selectedTheme = selection.Name;
        }
        else
        {
            selectedTheme = currentThemeName;
        }

        var selectedInfo = listResult.Installed.FirstOrDefault(t =>
            t.Metadata.Name.Equals(selectedTheme, StringComparison.OrdinalIgnoreCase));
        if (selectedInfo is null)
        {
            ErrorPanels.ShowError(
                "Theme Not Found",
                $"[yellow]Theme '{Markup.Escape(selectedTheme)}' is not installed.[/]");
            return 1;
        }

        if (promptForTheme)
        {
            var viewerChoices = new[]
            {
                new ViewerChoice(null, $"Theme default ({Canonical(selectedInfo.PhotoViewerCapabilities?.Default)})")
            }.Concat(selectedInfo.PhotoViewerCapabilities?.Supported.Select(mode =>
                new ViewerChoice(mode, Canonical(mode))) ?? []).ToList();
            var viewerSelection = AnsiConsole.Prompt(
                new SelectionPrompt<ViewerChoice>()
                    .Title("[cyan]Select photo viewer[/]")
                    .AddChoices(viewerChoices));
            viewer = viewerSelection.Mode;
            clearViewer = viewer is null;
        }

        var result = await themeService.UpdateAsync(
            new ThemeUpdateRequest(themeArg is null && selectedTheme.Equals(currentThemeName, StringComparison.OrdinalIgnoreCase)
                ? null
                : selectedTheme, viewer, clearViewer),
            cancellationToken);
        if (!result.Success)
        {
            ErrorPanels.ShowError("Theme Configuration Invalid", $"[yellow]{Markup.Escape(result.ErrorMessage ?? "Unknown error.")}[/]");
            return 1;
        }

        LogThemeChanged(currentThemeName, result.ThemeName);
        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Theme set to: [bold]{Markup.Escape(result.ThemeName)}[/]");
        return 0;
    }

    private static bool TryParseViewer(string value, out PhotoViewerMode? viewer)
    {
        viewer = PhotoViewerModeValues.TryParse(value, out var mode) ? mode : null;
        return viewer is not null;
    }

    private static string Canonical(PhotoViewerMode? mode) => (mode ?? PhotoViewerMode.None).ToValue();

    [LoggerMessage(Level = LogLevel.Information, Message = "Theme changed from '{OldTheme}' to '{NewTheme}'")]
    private partial void LogThemeChanged(string oldTheme, string newTheme);

    /// <summary>
    /// Represents a theme choice in the selection prompt.
    /// </summary>
    private sealed record ThemeChoice(string Name, string Source, bool IsCurrent)
    {
        public override string ToString() => IsCurrent
            ? $"{Markup.Escape(Name)} [dim]({Markup.Escape(Source)})[/] [green]← current[/]"
            : $"{Markup.Escape(Name)} [dim]({Markup.Escape(Source)})[/]";
    }

    private sealed record ViewerChoice(PhotoViewerMode? Mode, string Label)
    {
        public override string ToString() => Label;
    }
}








