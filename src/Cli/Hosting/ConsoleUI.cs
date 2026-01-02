using System.Reflection;

using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Shared console UI components for consistent branding across the application.
/// </summary>
/// <remarks>
/// Provides centralized UI elements used by both ProjectResolver (pre-host)
/// and InteractiveMenuService (post-host) for consistent look and feel.
/// </remarks>
internal static class ConsoleUI
{
    private static readonly string[] LogoLines =
    [
        @"   ____                _       ",
        @"  |  _ \ _____   _____| | __ _ ",
        @"  | |_) / _ \ \ / / _ \ |/ _` |",
        @"  |  _ <  __/\ V /  __/ | (_| |",
        @"  |_| \_\___| \_/ \___|_|\__,_|",
    ];

    /// <summary>
    /// Clears the console and displays the Revela ASCII logo.
    /// </summary>
    public static void ClearAndShowLogo()
    {
        AnsiConsole.Clear();
        ShowLogo();
    }

    /// <summary>
    /// Displays the Revela ASCII logo.
    /// </summary>
    public static void ShowLogo()
    {
        foreach (var line in LogoLines)
        {
            AnsiConsole.MarkupLine("[cyan1]" + line + "[/]");
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a welcome panel with version and optional project context.
    /// </summary>
    /// <param name="projectName">Project name to display (null for no project context)</param>
    /// <param name="folderName">Folder name to display when no project name is set</param>
    public static void ShowWelcomePanel(string? projectName = null, string? folderName = null)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";

        var lines = new List<string>
        {
            $"[bold]Version {version}[/]",
            "[dim]Modern static site generator for photographers[/]"
        };

        if (!string.IsNullOrEmpty(projectName))
        {
            lines.Add(string.Empty);
            lines.Add($"[blue]Project:[/] {projectName}");
        }
        else if (!string.IsNullOrEmpty(folderName))
        {
            lines.Add(string.Empty);
            lines.Add($"[dim]Directory:[/] {folderName}");
        }

        lines.Add(string.Empty);
        lines.Add("[blue]Navigate with[/] [bold]↑↓[/][blue], select with[/] [bold]Enter[/]");

        var panel = new Panel(new Markup(string.Join("\n", lines)))
            .WithHeader("[cyan1]Welcome[/]")
            .WithInfoStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Displays a first-run welcome panel for new installations.
    /// </summary>
    public static void ShowFirstRunPanel()
    {
        var panel = new Panel(
            new Markup(
                "[bold]Welcome to Revela![/]\n\n" +
                "This appears to be your first time running Revela.\n" +
                "The setup wizard will help you install themes and plugins."))
            .WithHeader("[cyan1]First Run[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a panel for when no project is configured in the current directory.
    /// </summary>
    public static void ShowNoProjectPanel()
    {
        var panel = new Panel(
            new Markup(
                "[bold]No Project Found[/]\n\n" +
                "This directory does not contain a Revela project.\n" +
                "Would you like to set one up?"))
            .WithHeader("[cyan1]Setup Required[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a goodbye message when exiting.
    /// </summary>
    public static void ShowGoodbye() => AnsiConsole.MarkupLine("[dim]Goodbye![/]");

    /// <summary>
    /// Shows a "press any key" prompt and waits for input.
    /// </summary>
    public static void WaitForKeyPress()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(true);
    }
}
