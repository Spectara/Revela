using System.CommandLine;

using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// The first-class <c>check</c> command group: an empty parent, a bespoke collect-all
/// <c>check all</c>, and host-wrapped <c>check &lt;name&gt;</c> sub-commands (one per
/// <see cref="ICheck"/>).
/// </summary>
/// <remarks>
/// <para>
/// <c>check all</c> (and the bare <c>check</c>) render one unified report over every
/// registered check via <see cref="ISiteValidator"/> — byte-for-byte the former standalone
/// <c>revela check</c> UX — and exit 2 when any check reports an error. Each
/// <c>check &lt;name&gt;</c> runs a single unit and prints only that unit's slice.
/// </para>
/// <para>
/// Checks are structural preconditions, not generate pipeline steps: they are deliberately
/// never <see cref="IPipelineStep"/>, so they cannot leak into <c>generate all</c>.
/// </para>
/// </remarks>
internal sealed partial class CheckCommand(
    ILogger<CheckCommand> logger,
    ISiteValidator validator)
{
    /// <summary>
    /// Creates the empty <c>check</c> parent command. Running it with no sub-command
    /// behaves like <c>check all</c>; sub-commands are added via host registration.
    /// </summary>
    public Command CreateParent()
    {
        var command = new Command("check", "Check your project for problems before generating");
        command.SetAction((parseResult, cancellationToken) =>
        {
            _ = parseResult;
            return RunAllAsync(cancellationToken);
        });

        return command;
    }

    /// <summary>
    /// Creates the bespoke collect-all <c>check all</c> command: it runs every check,
    /// merges the diagnostics, renders one unified report and exits 2 on any error.
    /// </summary>
    public Command CreateAll()
    {
        var command = new Command("all", "Run every check and report all problems at once");
        command.SetAction((parseResult, cancellationToken) =>
        {
            _ = parseResult;
            return RunAllAsync(cancellationToken);
        });

        return command;
    }

    /// <summary>
    /// Creates a host-wrapped <c>check &lt;name&gt;</c> command for a single check unit.
    /// Plugins never hand-write this — the host wraps every registered <see cref="ICheck"/>.
    /// </summary>
    /// <param name="check">The check to wrap.</param>
    public Command CreateUnit(ICheck check)
    {
        var command = new Command(check.Name, check.Title);
        command.SetAction((parseResult, cancellationToken) =>
        {
            _ = parseResult;
            return RunUnitAsync(check, cancellationToken);
        });

        return command;
    }

    private async Task<int> RunAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var diagnostics = await validator.ValidateAsync(cancellationToken);

            var errors = FormatBySeverity(diagnostics, ValidationSeverity.Error);
            var warnings = FormatBySeverity(diagnostics, ValidationSeverity.Warning);
            var hints = FormatBySeverity(diagnostics, ValidationSeverity.Hint);

            if (errors.Count > 0)
            {
                ErrorPanels.ShowValidationReport(errors, warnings, hints);
                LogCheckFailed(logger, errors.Count);
                return 2;
            }

            if (warnings.Count > 0 || hints.Count > 0)
            {
                ErrorPanels.ShowValidationReport(errors, warnings, hints);
            }

            ShowSuccessPanel(hasNotes: warnings.Count > 0 || hints.Count > 0);
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Canceled[/]");
            return 1;
        }
    }

    private async Task<int> RunUnitAsync(ICheck check, CancellationToken cancellationToken)
    {
        try
        {
            var diagnostics = await check.ValidateAsync(cancellationToken);

            var errors = FormatBySeverity(diagnostics, ValidationSeverity.Error);
            var warnings = FormatBySeverity(diagnostics, ValidationSeverity.Warning);
            var hints = FormatBySeverity(diagnostics, ValidationSeverity.Hint);

            if (errors.Count > 0)
            {
                ErrorPanels.ShowValidationReport(errors, warnings, hints);
                LogCheckFailed(logger, errors.Count);
                return 2;
            }

            if (warnings.Count > 0 || hints.Count > 0)
            {
                ErrorPanels.ShowValidationReport(errors, warnings, hints);
            }
            else
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} {Markup.Escape(check.Title)}: no problems found.");
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Canceled[/]");
            return 1;
        }
    }

    private static List<string> FormatBySeverity(IReadOnlyList<ValidationDiagnostic> diagnostics, ValidationSeverity severity) =>
        [.. diagnostics.Where(d => d.Severity == severity).Select(Format)];

    private static string Format(ValidationDiagnostic diagnostic)
    {
        var text = diagnostic.Message;

        if (!string.IsNullOrEmpty(diagnostic.File))
        {
            text += diagnostic.Line is int line
                ? $" ({diagnostic.File}:{line})"
                : $" ({diagnostic.File})";
        }

        if (!string.IsNullOrEmpty(diagnostic.Suggestion))
        {
            text += $" → {diagnostic.Suggestion}";
        }

        return text;
    }

    private static void ShowSuccessPanel(bool hasNotes)
    {
        var body = hasNotes
            ? "[green]No blocking problems — structure & configuration look good.[/]\n" +
              "[dim]See the notes above; they don't stop the build.[/]\n\n"
            : "[green]Structure & configuration look good.[/]\n\n";

        body +=
            "[dim]Your photos are checked later, during generate.[/]\n\n" +
            "[bold]Next step:[/]\n" +
            "  Run [cyan]revela generate all[/] to build your site.";

        var panel = new Panel(body)
            .WithHeader("[bold green]Check passed[/]")
            .WithSuccessStyle();

        AnsiConsole.Write(panel);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Check found {ErrorCount} blocking error(s)")]
    private static partial void LogCheckFailed(ILogger logger, int errorCount);
}
