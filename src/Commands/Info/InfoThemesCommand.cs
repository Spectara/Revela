using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

using Spectre.Console;

namespace Spectara.Revela.Commands.Info;

/// <summary>
/// Lists installed themes with version, source, and the active marker.
/// </summary>
/// <remarks>
/// The active theme is read from <see cref="ThemeConfig.Name"/> and only
/// highlighted when a project is loaded. Theme authors may add per-theme
/// detail subcommands via <c>ParentCommand: "info themes"</c>.
/// </remarks>
internal sealed class InfoThemesCommand(
    IPackageContext packageContext,
    IOptionsMonitor<ThemeConfig> themeConfig)
{
    /// <summary>Creates the command definition.</summary>
    public Command Create()
    {
        var command = new Command("themes", "List installed themes");
        command.SetAction(_ => Execute());
        return command;
    }

    private int Execute()
    {
        var themes = packageContext.Themes
            .OrderBy(t => t.Theme.Metadata.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var activeName = themeConfig.CurrentValue.Name;

        if (themes.Count == 0)
        {
            var emptyPanel = new Panel(new Markup("[dim]No themes loaded.[/]"))
                .WithHeader("[bold]Themes[/] [dim](0)[/]")
                .WithInfoStyle();
            emptyPanel.Padding = new Padding(1, 0, 1, 0);
            AnsiConsole.Write(emptyPanel);
            return 0;
        }

        var lines = new List<string>();
        foreach (var info in themes)
        {
            var meta = info.Theme.Metadata;
            var sourceMarkup = FormatSource(info.Source);
            var isActive = !string.IsNullOrEmpty(activeName)
                && string.Equals(meta.Name, activeName, StringComparison.OrdinalIgnoreCase);
            var marker = isActive ? "[yellow]★[/] " : "  ";

            lines.Add($"{marker}[bold green]{Markup.Escape(meta.Name)}[/] [dim]v{Markup.Escape(meta.Version)}[/] {sourceMarkup}");
            if (!string.IsNullOrWhiteSpace(meta.Description))
            {
                lines.Add($"     [dim]{Markup.Escape(meta.Description)}[/]");
            }
            lines.Add($"     [dim]{Markup.Escape(meta.Id)}[/]");
            lines.Add(string.Empty);
        }

        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var panel = new Panel(new Markup(string.Join("\n", lines)))
            .WithHeader($"[bold]Themes[/] [dim]({themes.Count.ToString(CultureInfo.InvariantCulture)})[/]")
            .WithInfoStyle();
        panel.Padding = new Padding(1, 0, 1, 0);
        AnsiConsole.Write(panel);

        if (string.IsNullOrEmpty(activeName))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim](no active theme — open a Revela project to see the active marker)[/]");
        }

        return 0;
    }

    private static string FormatSource(PackageSource source) => source switch
    {
        PackageSource.Bundled => "[grey][[bundled]][/]",
        PackageSource.Local => "[cyan][[local]][/]",
        _ => $"[grey][[{source}]][/]",
    };
}
