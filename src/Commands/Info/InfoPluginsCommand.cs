using System.CommandLine;
using System.Globalization;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

using Spectre.Console;

namespace Spectara.Revela.Commands.Info;

/// <summary>
/// Lists installed plugins with version and source.
/// </summary>
/// <remarks>
/// Plugin authors may add per-plugin detail subcommands via
/// <c>ParentCommand: "info plugins"</c>; those appear under <c>revela info
/// plugins &lt;name&gt;</c> and as sub-entries in the TUI menu. They should
/// be read-only diagnostics (no prompts), kept compact for bug-report
/// copy-paste.
/// </remarks>
internal sealed class InfoPluginsCommand(IPackageContext packageContext)
{
    /// <summary>Creates the command definition.</summary>
    public Command Create()
    {
        var command = new Command("plugins", "List installed plugins");
        command.SetAction(_ => Execute());
        return command;
    }

    private int Execute()
    {
        var plugins = packageContext.Plugins
            .OrderBy(p => p.Plugin.Metadata.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (plugins.Count == 0)
        {
            var emptyPanel = new Panel(new Markup("[dim]No plugins loaded.[/]"))
                .WithHeader("[bold]Plugins[/] [dim](0)[/]")
                .WithInfoStyle();
            emptyPanel.Padding = new Padding(1, 0, 1, 0);
            AnsiConsole.Write(emptyPanel);
            return 0;
        }

        var lines = new List<string>();
        foreach (var info in plugins)
        {
            var meta = info.Plugin.Metadata;
            var sourceMarkup = FormatSource(info.Source);
            lines.Add($"[bold green]{Markup.Escape(meta.Name)}[/] [dim]v{Markup.Escape(meta.Version)}[/] {sourceMarkup}");
            if (!string.IsNullOrWhiteSpace(meta.Description))
            {
                lines.Add($"   [dim]{Markup.Escape(meta.Description)}[/]");
            }
            lines.Add($"   [dim]{Markup.Escape(meta.Id)}[/]");
            lines.Add(string.Empty);
        }

        // Drop trailing blank
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var panel = new Panel(new Markup(string.Join("\n", lines)))
            .WithHeader($"[bold]Plugins[/] [dim]({plugins.Count.ToString(CultureInfo.InvariantCulture)})[/]")
            .WithInfoStyle();
        panel.Padding = new Padding(1, 0, 1, 0);
        AnsiConsole.Write(panel);

        return 0;
    }

    private static string FormatSource(PackageSource source) => source switch
    {
        PackageSource.Bundled => "[grey][[bundled]][/]",
        PackageSource.Local => "[cyan][[local]][/]",
        _ => $"[grey][[{source}]][/]",
    };
}
