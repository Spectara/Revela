using System.CommandLine;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;
using Spectre.Console;

namespace Spectara.Revela.Features.Theme.Commands;

/// <summary>
/// Command to list available themes — thin UI wrapper around <see cref="IThemeService"/>.
/// </summary>
internal sealed partial class ThemeListCommand(IThemeService themeService)
{
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
            await ExecuteAsync(online, cancellationToken);
            return 0;
        });

        return command;
    }

    private async Task ExecuteAsync(bool includeOnline, CancellationToken cancellationToken)
    {
        var result = await themeService.ListAsync(includeOnline, cancellationToken);

        if (result.Installed.Count == 0)
        {
            ErrorPanels.ShowNothingInstalledError(
                "themes",
                "theme install Spectara.Revela.Themes.Lumina",
                "theme list --online");
        }
        else
        {
            ShowInstalledThemes(result.Installed, cancellationToken);
        }

        if (result.Online.Count > 0)
        {
            ShowOnlineThemes(result.Online, cancellationToken);
        }
    }

    private static void ShowInstalledThemes(
        IReadOnlyList<ThemeInfo> themes,
        CancellationToken cancellationToken)
    {
        var content = new List<string>();

        foreach (var theme in themes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceIcon = theme.IsLocal ? "[blue]*[/]" : "[green]+[/]";
            var sourceMarkup = GetSourceMarkup(theme);

            content.Add($"{sourceIcon} [bold green]{Markup.Escape(theme.Metadata.Name)}[/] [dim]v{theme.Metadata.Version}[/] {sourceMarkup}");
            content.Add($"   [dim]{Markup.Escape(theme.Metadata.Description)}[/]");

            if (theme.IsLocal)
            {
                content.Add($"   [blue]Source: themes/{Markup.Escape(theme.Metadata.Name)}/[/]");
            }

            foreach (var ext in theme.Extensions)
            {
                var extSourceMarkup = GetExtSourceMarkup(ext);
                content.Add($"   [dim]└─[/] [cyan]{Markup.Escape(ext.Metadata.Name)}[/] [dim]v{ext.Metadata.Version}[/] {extSourceMarkup}");
            }

            content.Add("");
        }

        if (content.Count > 0 && string.IsNullOrEmpty(content[^1]))
        {
            content.RemoveAt(content.Count - 1);
        }

        var panel = new Panel(new Markup(string.Join("\n", content)))
            .WithHeader($"[bold]Installed Themes[/] [dim]({themes.Count})[/]")
            .WithInfoStyle();
        panel.Padding = new Padding(1, 0, 1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[dim]Tip:[/] Use [cyan]revela theme extract <name>[/] to customize a theme");
    }

    private static void ShowOnlineThemes(
        IReadOnlyList<OnlineThemeInfo> themes,
        CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("");

        var content = new List<string>();

        foreach (var theme in themes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var statusIcon = theme.IsInstalled ? OutputMarkers.Success : "[dim]○[/]";

            content.Add($"{statusIcon} [bold]{Markup.Escape(theme.Name)}[/] [dim]v{theme.Version}[/]");

            if (!string.IsNullOrEmpty(theme.Description))
            {
                content.Add($"   [dim]{Markup.Escape(theme.Description)}[/]");
            }

            if (!theme.IsInstalled)
            {
                content.Add($"   Install: [cyan]revela theme install {theme.Id}[/]");
            }

            content.Add("");
        }

        if (content.Count > 0 && string.IsNullOrEmpty(content[^1]))
        {
            content.RemoveAt(content.Count - 1);
        }

        var panel = new Panel(new Markup(string.Join("\n", content)))
            .WithHeader($"[bold]Available from NuGet[/] [dim]({themes.Count})[/]")
            .WithInfoStyle();
        panel.Padding = new Padding(1, 0, 1, 0);

        AnsiConsole.Write(panel);
    }

    private static string GetSourceMarkup(ThemeInfo theme)
    {
        if (theme.IsLocal)
        {
            return "[blue]themes/[/]";
        }

        return theme.Source switch
        {
            PackageSource.Bundled => "[magenta]bundled[/]",
            PackageSource.Local => "[green]installed[/]",
            _ => "[dim]installed[/]"
        };
    }

    private static string GetExtSourceMarkup(ThemeExtensionInfo ext) =>
        ext.Source switch
        {
            PackageSource.Bundled => "[magenta]bundled[/]",
            PackageSource.Local => "[green]installed[/]",
            _ => "[dim]installed[/]"
        };
}
