using System.CommandLine;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Theme;

/// <summary>
/// Command to configure the theme.
/// </summary>
/// <remarks>
/// Shows available themes and allows selection.
/// Validates that selected theme exists.
/// </remarks>
public sealed partial class ConfigThemeCommand(
    ILogger<ConfigThemeCommand> logger,
    IOptionsMonitor<ThemeConfig> themeConfig,
    IConfigService configService,
    IThemeResolver themeResolver,
    IPluginContext pluginContext)
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
            ErrorPanels.ShowNotAProjectError();
            return 1;
        }

        // Get current theme from IOptions (runtime reading)
        var currentTheme = themeConfig.CurrentValue.Name;
        if (string.IsNullOrWhiteSpace(currentTheme))
        {
            currentTheme = null; // Treat empty as unset
        }

        // Get available themes
        var projectPath = Directory.GetCurrentDirectory();
        var availableThemes = themeResolver.GetAvailableThemes(projectPath).ToList();

        if (availableThemes.Count == 0)
        {
            ErrorPanels.ShowNothingInstalledError(
                "themes",
                "theme install Spectara.Revela.Theme.Lumina",
                "theme list --online");
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
                var availableList = string.Join("\n", availableThemes.Select(t => $"  [green]{t.Metadata.Name}[/] [dim]({GetThemeSource(t)})[/]"));
                ErrorPanels.ShowError(
                    "Theme Not Found",
                    $"[yellow]Theme '{themeArg}' not found.[/]\n\n" +
                    $"[bold]Available themes:[/]\n{availableList}");
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

        // Save theme using JsonObject (theme section with name property)
        var update = new JsonObject
        {
            ["theme"] = new JsonObject { ["name"] = selectedTheme }
        };
        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        // Note: ConfigService automatically invalidates IOptionsMonitor caches after write

        LogThemeChanged(currentTheme ?? "none", selectedTheme);
        AnsiConsole.MarkupLine($"[green]✓[/] Theme set to: [bold]{selectedTheme}[/]");

        return 0;
    }

    private string GetThemeSource(IThemePlugin theme)
    {
        // Check for local themes first
        var typeName = theme.GetType().Name;
        if (typeName.Contains("LocalThemeAdapter", StringComparison.Ordinal))
        {
            return "local";
        }

        // Look up source from plugin context
        var pluginInfo = pluginContext.Plugins
            .FirstOrDefault(p => p.Plugin.Metadata.Name.Equals(theme.Metadata.Name, StringComparison.OrdinalIgnoreCase));

        return pluginInfo?.Source switch
        {
            PluginSource.Bundled => "bundled",
            PluginSource.Local => "installed",
            _ => "installed"
        };
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
