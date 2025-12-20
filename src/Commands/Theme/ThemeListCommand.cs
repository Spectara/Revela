using System.CommandLine;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Command to list available themes.
/// </summary>
/// <remarks>
/// Shows themes from three sources:
/// 1. Local themes (project/themes/ folder)
/// 2. Installed theme plugins (built-in + plugins/)
/// 3. Available themes from NuGet (with --online flag)
/// </remarks>
public sealed partial class ThemeListCommand(
    IThemeResolver themeResolver,
    PluginManager pluginManager)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    /// <returns>The configured list command.</returns>
    public Command Create()
    {
        var command = new Command("list", "List available themes");

        var onlineOption = new Option<bool>("--online", "-o")
        {
            Description = "Also show themes available from NuGet sources"
        };
        command.Options.Add(onlineOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var online = parseResult.GetValue(onlineOption);
            await ExecuteAsync(online, cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    private async Task ExecuteAsync(bool includeOnline, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var projectPath = Environment.CurrentDirectory;
        var themes = themeResolver.GetAvailableThemes(projectPath).ToList();

        // Show installed themes
        if (themes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No themes installed.[/]");
        }
        else
        {
            ShowInstalledThemes(themes, cancellationToken);
        }

        // Show online themes if requested
        if (includeOnline)
        {
            await ShowOnlineThemesAsync(themes, cancellationToken).ConfigureAwait(false);
        }
        else if (themes.Count == 0)
        {
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("To see available themes from NuGet:");
            AnsiConsole.MarkupLine("  [cyan]revela theme list --online[/]");
        }
    }

    private static void ShowInstalledThemes(List<IThemePlugin> themes, CancellationToken cancellationToken)
    {
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
            Header = new PanelHeader($"[bold]Installed Themes[/] [dim]({themes.Count})[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[dim]Tip:[/] Use [cyan]revela theme extract <name>[/] to customize a theme");
    }

    private async Task ShowOnlineThemesAsync(List<IThemePlugin> installedThemes, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("");

        var onlineThemes = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[yellow]Searching NuGet sources...[/]", async _ =>
            {
                return await pluginManager.SearchPackagesAsync(
                    "Spectara.Revela.Theme",
                    packageTypeFilter: "RevelaTheme",
                    includePrerelease: false,
                    cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

        if (onlineThemes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No themes found in NuGet sources.[/]");
            return;
        }

        // Filter out already installed themes
        var installedNames = installedThemes
            .Select(t => t.Metadata.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableThemes = onlineThemes
            .Where(t => !installedNames.Contains(ExtractThemeName(t.Id)))
            .ToList();

        // Build content for panel
        var content = new List<string>();

        foreach (var theme in onlineThemes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var themeName = ExtractThemeName(theme.Id);
            var isInstalled = installedNames.Contains(themeName);
            var statusIcon = isInstalled ? "[green]✓[/]" : "[dim]○[/]";

            content.Add($"{statusIcon} [bold]{EscapeMarkup(themeName)}[/] [dim]v{theme.Version}[/] [dim]({theme.SourceName})[/]");

            if (!string.IsNullOrEmpty(theme.Description))
            {
                content.Add($"   [dim]{EscapeMarkup(theme.Description)}[/]");
            }

            if (!isInstalled)
            {
                content.Add($"   Install: [cyan]revela plugin install {theme.Id}[/]");
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
            Header = new PanelHeader($"[bold]Available from NuGet[/] [dim]({onlineThemes.Count})[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
    }

    private static string GetThemeSource(IThemePlugin theme)
    {
        // Check if it's a local theme by type name
        var typeName = theme.GetType().Name;

        return typeName.Contains("LocalThemeAdapter", StringComparison.Ordinal)
            ? "local"
            : "installed";
    }

    /// <summary>
    /// Extracts the theme name from a package ID.
    /// </summary>
    /// <example>Spectara.Revela.Theme.Lumina → Lumina</example>
    private static string ExtractThemeName(string packageId)
    {
        const string prefix = "Spectara.Revela.Theme.";
        return packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? packageId[prefix.Length..]
            : packageId;
    }

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }
}
