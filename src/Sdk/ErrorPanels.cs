using Spectre.Console;

namespace Spectara.Revela.Sdk;

/// <summary>
/// Reusable error panel templates for consistent CLI error messages.
/// </summary>
public static class ErrorPanels
{
    /// <summary>
    /// Shows an error panel when a command is run outside a Revela project.
    /// </summary>
    public static void ShowNotAProjectError()
    {
        var panel = new Panel(
            "[yellow]This command requires a Revela project.[/]\n\n" +
            "[bold]Solution:[/]\n" +
            "  Run [cyan]revela config project[/] to initialize a new project.\n\n" +
            "[dim]A Revela project needs:[/]\n" +
            "  • [cyan]project.json[/] - Project settings\n" +
            "  • [cyan]site.json[/] - Site configuration (theme-dependent)"
        )
        .WithHeader("[bold red]Not a Revela Project[/]")
        .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel when configuration is missing.
    /// </summary>
    /// <param name="what">What is missing (e.g., "OneDrive share URL").</param>
    /// <param name="configCommand">The config command to run (e.g., "config onedrive").</param>
    /// <param name="additionalHints">Optional additional solution hints.</param>
    public static void ShowConfigRequiredError(
        string what,
        string configCommand,
        string? additionalHints = null)
    {
        var content = $"[yellow]{what} not configured.[/]\n\n" +
            $"[bold]Quick fix:[/]\n" +
            $"  Run [cyan]revela {configCommand}[/] to configure interactively.";

        if (!string.IsNullOrWhiteSpace(additionalHints))
        {
            content += $"\n\n[dim]Or:[/]\n{additionalHints}";
        }

        var panel = new Panel(content)
            .WithHeader("[bold red]Configuration Required[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel when a prerequisite step is missing.
    /// </summary>
    /// <param name="what">What is missing (e.g., "Site manifest").</param>
    /// <param name="prerequisiteCommand">The command to run first (e.g., "generate scan").</param>
    /// <param name="explanation">Optional explanation of why this is needed.</param>
    public static void ShowPrerequisiteError(
        string what,
        string prerequisiteCommand,
        string? explanation = null)
    {
        var content = $"[yellow]{what} not found.[/]";

        if (!string.IsNullOrWhiteSpace(explanation))
        {
            content += $"\n\n[dim]{explanation}[/]";
        }

        content += $"\n\n[bold]Solution:[/]\n" +
            $"  Run [cyan]revela {prerequisiteCommand}[/] first.";

        var panel = new Panel(content)
            .WithHeader("[bold red]Prerequisite Missing[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel when no items are found (e.g., no themes installed).
    /// </summary>
    /// <param name="what">What was not found (e.g., "themes").</param>
    /// <param name="installCommand">The command to install (e.g., "theme install Spectara.Revela.Theme.Lumina").</param>
    /// <param name="listCommand">Optional command to list available items (e.g., "theme list --online").</param>
    public static void ShowNothingInstalledError(
        string what,
        string installCommand,
        string? listCommand = null)
    {
        var content = $"[yellow]No {what} installed.[/]\n\n" +
            $"[bold]Solution:[/]\n" +
            $"  Run [cyan]revela {installCommand}[/] to install.";

        if (!string.IsNullOrWhiteSpace(listCommand))
        {
            content += $"\n\n[dim]To see available {what}:[/]\n" +
                $"  Run [cyan]revela {listCommand}[/]";
        }

        var panel = new Panel(content)
            .WithHeader($"[bold yellow]No {what.ToUpperInvariant()} Found[/]")
            .WithWarningStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel for a generic error with custom message.
    /// </summary>
    /// <param name="title">The error title.</param>
    /// <param name="message">The error message (can include Spectre markup).</param>
    public static void ShowError(string title, string message)
    {
        var panel = new Panel(message)
            .WithHeader($"[bold red]{title}[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows a warning panel for non-fatal issues.
    /// </summary>
    /// <param name="title">The warning title.</param>
    /// <param name="message">The warning message (can include Spectre markup).</param>
    public static void ShowWarning(string title, string message)
    {
        var panel = new Panel(message)
            .WithHeader($"[bold yellow]{title}[/]")
            .WithWarningStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel for exceptions with the exception message.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="hint">Optional hint for how to resolve the issue.</param>
    public static void ShowException(Exception ex, string? hint = null)
    {
        var content = $"[yellow]{EscapeMarkup(ex.Message)}[/]";

        if (!string.IsNullOrWhiteSpace(hint))
        {
            content += $"\n\n[dim]{hint}[/]";
        }

        var panel = new Panel(content)
            .WithHeader("[bold red]Error[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel when a validation fails.
    /// </summary>
    /// <param name="message">The validation error message.</param>
    /// <param name="hint">Optional hint for valid values.</param>
    public static void ShowValidationError(string message, string? hint = null)
    {
        var content = $"[yellow]{message}[/]";

        if (!string.IsNullOrWhiteSpace(hint))
        {
            content += $"\n\n[bold]Valid values:[/]\n{hint}";
        }

        var panel = new Panel(content)
            .WithHeader("[bold red]Validation Error[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel when a file already exists.
    /// </summary>
    /// <param name="path">The path that already exists.</param>
    /// <param name="hint">Optional hint for next steps.</param>
    public static void ShowFileExistsError(string path, string? hint = null)
    {
        var content = $"[yellow]File already exists:[/] [cyan]{EscapeMarkup(path)}[/]";

        if (!string.IsNullOrWhiteSpace(hint))
        {
            content += $"\n\n[dim]{hint}[/]";
        }

        var panel = new Panel(content)
            .WithHeader("[bold red]File Exists[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel when a directory is not found.
    /// </summary>
    /// <param name="path">The path that was not found.</param>
    /// <param name="prerequisiteCommand">Optional command to create the directory.</param>
    public static void ShowDirectoryNotFoundError(string path, string? prerequisiteCommand = null)
    {
        var content = $"[yellow]Directory not found:[/] [cyan]{EscapeMarkup(path)}[/]";

        if (!string.IsNullOrWhiteSpace(prerequisiteCommand))
        {
            content += $"\n\n[bold]Solution:[/]\n  Run [cyan]revela {prerequisiteCommand}[/] first.";
        }

        var panel = new Panel(content)
            .WithHeader("[bold red]Directory Not Found[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel when a port is unavailable.
    /// </summary>
    /// <param name="port">The port that is unavailable.</param>
    /// <param name="reason">The reason (e.g., "in use", "access denied").</param>
    /// <param name="hint">Optional hint for resolution.</param>
    public static void ShowPortError(int port, string reason, string? hint = null)
    {
        var content = $"[yellow]Port {port} {reason}.[/]";

        if (!string.IsNullOrWhiteSpace(hint))
        {
            content += $"\n\n[dim]{hint}[/]";
        }

        var panel = new Panel(content)
            .WithHeader("[bold red]Port Unavailable[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Escapes Spectre markup characters in user-provided text.
    /// </summary>
    private static string EscapeMarkup(string text) =>
        text.Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
}
