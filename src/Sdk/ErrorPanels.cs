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
    /// <param name="installCommand">The command to install (e.g., "theme install Spectara.Revela.Themes.Lumina").</param>
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
        var content = $"[yellow]{Markup.Escape(ex.Message)}[/]";

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
    /// Shows an error panel for invalid configuration values, listing each
    /// failure in plain language with no stack trace.
    /// </summary>
    /// <remarks>
    /// Used at the CLI boundary to turn a configuration validation failure
    /// (e.g. a stray setting in <c>project.json</c>) into a photographer-readable
    /// message instead of a raw .NET exception dump.
    /// </remarks>
    /// <param name="problems">The human-readable validation failure messages.</param>
    public static void ShowConfigurationProblem(IEnumerable<string> problems)
    {
        var lines = problems
            .Where(problem => !string.IsNullOrWhiteSpace(problem))
            .Select(problem => $"  • [yellow]{Markup.Escape(problem)}[/]")
            .ToList();

        var body = lines.Count > 0
            ? string.Join("\n", lines)
            : "  • [yellow]A configuration value is not valid.[/]";

        var content =
            "Revela can't continue because your project configuration isn't valid:\n\n" +
            body +
            "\n\n[dim]Fix the setting in your project.json (or site.json), then run the command again.[/]";

        var panel = new Panel(content)
            .WithHeader("[bold red]Configuration problem[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows an error panel when a configuration <em>file</em> can't be parsed because
    /// it contains a JSON syntax error (e.g. junk before the JSON), naming the offending
    /// file and, when known, the location of the problem.
    /// </summary>
    /// <remarks>
    /// Used at the CLI boundary to turn an eager configuration-load failure
    /// (<c>revela.json</c> / <c>project.json</c> / <c>site.json</c> / <c>logging.json</c>)
    /// into a photographer-readable message instead of a raw .NET exception dump. Mirrors
    /// the look and escaping of <see cref="ShowConfigurationProblem"/>.
    /// </remarks>
    /// <param name="path">Path (or friendly phrase) identifying the malformed file.</param>
    /// <param name="line">Zero-based line number reported by the JSON parser, if known.</param>
    /// <param name="position">Zero-based byte position within the line, if known.</param>
    public static void ShowConfigFileError(string path, long? line, long? position)
    {
        var content =
            $"Revela can't read your configuration file: [cyan]{Markup.Escape(path)}[/].\n\n";

        if (line is { } lineNumber)
        {
            content += position is { } columnNumber
                ? $"There's a JSON syntax error near line {lineNumber + 1}, column {columnNumber + 1}."
                : $"There's a JSON syntax error near line {lineNumber + 1}.";
        }
        else
        {
            content += "There's a JSON syntax error in the file.";
        }

        content += "\n\n[dim]Fix the file and run the command again.[/]";

        var panel = new Panel(content)
            .WithHeader("[bold red]Configuration problem[/]")
            .WithErrorStyle();

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows a single panel grouping validation findings by severity (errors, warnings,
    /// hints), reusing the configuration-problem panel look.
    /// </summary>
    /// <remarks>
    /// The panel border reflects the highest severity present: red when any error exists,
    /// yellow when only warnings do, cyan when only hints. Errors make <c>revela check</c>
    /// exit with code 2; warnings and hints do not. Each message is plain text and is escaped here.
    /// </remarks>
    /// <param name="errors">Problems that must be fixed (empty when none).</param>
    /// <param name="warnings">Questionable items (empty when none).</param>
    /// <param name="hints">Friendly, informational notes (empty when none).</param>
    public static void ShowValidationReport(
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> hints)
    {
        var sections = new List<string>();

        if (errors.Count > 0)
        {
            sections.Add(FormatSection($"Errors ({errors.Count}) — must be fixed:", errors, "red"));
        }

        if (warnings.Count > 0)
        {
            sections.Add(FormatSection($"Warnings ({warnings.Count}) — build still runs:", warnings, "yellow"));
        }

        if (hints.Count > 0)
        {
            sections.Add(FormatSection($"Hints ({hints.Count}):", hints, "blue"));
        }

        if (sections.Count == 0)
        {
            return;
        }

        var content = string.Join("\n\n", sections);

        Panel panel;
        if (errors.Count > 0)
        {
            content += "\n\n[dim]Fix the errors above, then run the command again.[/]";
            panel = new Panel(content)
                .WithHeader("[bold red]Check found problems[/]")
                .WithErrorStyle();
        }
        else if (warnings.Count > 0)
        {
            panel = new Panel(content)
                .WithHeader("[bold yellow]Check: warnings[/]")
                .WithWarningStyle();
        }
        else
        {
            panel = new Panel(content)
                .WithHeader("[bold cyan]Check: hints[/]")
                .WithInfoStyle();
        }

        AnsiConsole.Write(panel);
    }

    private static string FormatSection(string heading, IReadOnlyList<string> items, string color)
    {
        var lines = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => $"  • [{color}]{Markup.Escape(item)}[/]");

        return $"[bold]{heading}[/]\n" + string.Join("\n", lines);
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
        var content = $"[yellow]File already exists:[/] [cyan]{Markup.Escape(path)}[/]";

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
        var content = $"[yellow]Directory not found:[/] [cyan]{Markup.Escape(path)}[/]";

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
    /// Shows an info panel when a restart is required after installing packages.
    /// </summary>
    /// <param name="what">What was installed (e.g., "plugins", "themes").</param>
    public static void ShowRestartRequired(string what)
    {
        var panel = new Panel(
            $"The installed {what} will be available after restarting Revela.")
            .WithHeader("[bold yellow]Restart Required[/]")
            .WithWarningStyle();

        AnsiConsole.Write(panel);
    }
}
