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
    /// <summary>
    /// Highlight style for simple selection prompts (cyan).
    /// </summary>
    internal static readonly Style PromptHighlightStyle = new(Color.Cyan1);

    /// <summary>
    /// Bold highlight style for grouped selection prompts (cyan + bold).
    /// </summary>
    internal static readonly Style PromptBoldHighlightStyle = new(Color.Cyan1, decoration: Decoration.Bold);

    /// <summary>
    /// Disabled/dimmed style for non-selectable group headers in selection prompts.
    /// </summary>
    internal static readonly Style GroupHeaderStyle = new(Color.Grey);

    /// <summary>
    /// Clears the console (no banner).
    /// </summary>
    /// <remarks>
    /// Version data is no longer shown at startup — use <c>revela info</c>
    /// (CLI) or the <c>Info</c> menu group (TUI) for version and host details.
    /// </remarks>
    public static void ClearConsole() => AnsiConsole.Clear();

    /// <summary>
    /// Displays a compact welcome panel with optional project context.
    /// </summary>
    /// <param name="projectName">Project name to display (null for no project context).</param>
    /// <param name="folderName">Folder name to display when no project name is set.</param>
    public static void ShowWelcomePanel(string? projectName = null, string? folderName = null)
    {
        var lines = new List<string>();

        if (!string.IsNullOrEmpty(projectName))
        {
            lines.Add($"[blue]Project:[/] {Markup.Escape(projectName)}");
        }
        else if (!string.IsNullOrEmpty(folderName))
        {
            lines.Add($"[dim]Directory:[/] {Markup.Escape(folderName)}");
        }

        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add("[blue]Navigate with[/] [bold]↑↓[/][blue], select with[/] [bold]Enter[/]");

        var panel = new Panel(new Markup(string.Join("\n", lines)))
            .WithHeader("[cyan1]Revela[/]")
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
            .WithInfoStyle()
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

}
