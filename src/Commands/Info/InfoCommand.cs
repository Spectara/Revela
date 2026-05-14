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
/// kind, plugin/theme counts, active theme). Subcommands <c>plugins</c> and
/// <c>themes</c> show detail tables. Plugins may register additional detail
/// commands via <c>ParentCommand: "info plugins"</c>.
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

        if (buildInfo.Kind == HostKind.Embedded)
        {
            AnsiConsole.MarkupLine(
                "[dim]Plugin management: not available in embedded build (use the standalone CLI)[/]");
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
        AnsiConsole.MarkupLine("[dim]For details: [white]revela info plugins[/] · [white]revela info themes[/][/]");

        return 0;
    }
}
