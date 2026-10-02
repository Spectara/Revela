using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;

using Spectre.Console;

namespace Spectara.Revela.Commands.Info;

/// <summary>
/// Parent command for Revela diagnostic information.
/// </summary>
/// <remarks>
/// <para>
/// Default action prints a compact Revela summary (version, framework, host
/// kind, plugin/theme counts, active theme). In the Full edition package
/// details live in <c>revela plugin list</c> and <c>revela theme list</c>; the
/// Standalone edition has no <c>plugin</c> command, so it lists its built-in
/// plugins and themes (name and version) here.
/// </para>
/// <para>
/// The first line of output is <see cref="IBuildInfo.FormatVersionLine"/>
/// — the same string printed by <c>revela --version</c>.
/// </para>
/// </remarks>
internal sealed class InfoCommand(
    IBuildInfo buildInfo,
    IPackageContext packageContext,
    IOptionsMonitor<ThemeConfig> themeConfig)
{
    /// <summary>Creates the command definition.</summary>
    public Command Create()
    {
        var command = new Command("info", "Show Revela version and host info");
        command.SetAction(_ => Execute());
        return command;
    }

    private int Execute()
    {
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(buildInfo.FormatVersionLine())}[/]");

        if (buildInfo.Kind == HostKind.Standalone)
        {
            AnsiConsole.MarkupLine(
                "[dim]Package management: not available in the Standalone edition (use the Full edition to install plugins and themes)[/]");
        }

        AnsiConsole.WriteLine();

        var pluginCount = packageContext.Plugins.Count;
        var themeCount = packageContext.Themes.Count;
        var activeThemeName = themeConfig.CurrentValue.Name;

        var summary = new List<string>
        {
            $"[blue]Plugins:[/]      {pluginCount.ToString(CultureInfo.InvariantCulture)}",
            $"[blue]Themes:[/]       {themeCount.ToString(CultureInfo.InvariantCulture)}",
            string.IsNullOrEmpty(activeThemeName)
                ? "[blue]Active theme:[/] [dim](no project)[/]"
                : $"[blue]Active theme:[/] {Markup.Escape(activeThemeName)}",
            string.Empty,
            $"[dim]Build:[/]        {Markup.Escape(buildInfo.Configuration)} ({Markup.Escape(buildInfo.RuntimeIdentifier)})",
            $"[dim]Build id:[/]     {Markup.Escape(buildInfo.InformationalVersion)}",
        };

        var panel = new Panel(new Markup(string.Join("\n", summary)))
            .WithHeader("[bold]Revela[/]")
            .WithInfoStyle();
        panel.Padding = new Padding(1, 0, 1, 0);
        AnsiConsole.Write(panel);

        AnsiConsole.WriteLine();

        // Standalone has no `plugin list`: info is the only place that shows what is built in
        if (buildInfo.Kind == HostKind.Standalone)
        {
            WritePackageTable("Included plugins", packageContext.Plugins.Select(p => p.Plugin.Metadata));
            WritePackageTable("Included themes", packageContext.Themes.Select(t => t.Theme.Metadata));
            AnsiConsole.MarkupLine("[dim]Theme files and local themes: [white]revela theme list[/][/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]For details: [white]revela plugin list[/] · [white]revela theme list[/][/]");
        }

        return 0;
    }

    private static void WritePackageTable(string title, IEnumerable<PackageMetadata> packages)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]{Markup.Escape(title)}[/]")
            .AddColumn("Name")
            .AddColumn("Version");

        foreach (var metadata in packages.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Markup.Escape(metadata.Name), Markup.Escape(metadata.Version));
        }

        if (table.Rows.Count == 0)
        {
            table.AddRow("[dim](none)[/]", string.Empty);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }
}
