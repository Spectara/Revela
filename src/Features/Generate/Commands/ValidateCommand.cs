using System.CommandLine;

using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

using Spectre.Console;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// Runs the shared <see cref="ISiteValidator"/> as both a standalone <c>revela check</c>
/// command and the invisible Phase 0 of <c>generate all</c>.
/// </summary>
/// <remarks>
/// <para>
/// Three severities are surfaced: errors block (exit code 2), warnings and hints are shown
/// but never abort the build (exit code 0). As an <see cref="IPipelineStep"/> the step runs
/// first in the generate pipeline and fails fast on errors — before the expensive image
/// step ever runs.
/// </para>
/// <para>
/// The standalone command prints a scope-honest success message (structure and configuration
/// only — photos are checked later, during generate) and points at the next step.
/// </para>
/// </remarks>
internal sealed partial class ValidateCommand(
    ILogger<ValidateCommand> logger,
    ISiteValidator validator) : IPipelineStep
{
    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Generate;

    string IPipelineStep.Name => "check";

    async ValueTask<PipelineStepResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        var diagnostics = await validator.ValidateAsync(cancellationToken);
        var errorCount = diagnostics.Count(d => d.Severity == ValidationSeverity.Error);

        return errorCount == 0
            ? PipelineStepResult.Ok()
            : PipelineStepResult.Fail($"Validation found {errorCount} error(s). Run 'revela check' for details.");
    }

    // ── CLI commands ──

    /// <summary>
    /// Creates the standalone <c>check</c> root command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("check", "Check project structure and configuration before generating");
        command.SetAction((parseResult, cancellationToken) =>
        {
            _ = parseResult;
            return RunAsync(standalone: true, cancellationToken);
        });

        return command;
    }

    /// <summary>
    /// Creates the hidden <c>generate check</c> step command that becomes Phase 0 of
    /// <c>generate all</c>. Hidden so photographers never have to learn it — validation is
    /// simply the first, silent-on-success phase of generating.
    /// </summary>
    public Command CreateStep()
    {
        var command = new Command("check", "Validate project structure and configuration")
        {
            Hidden = true,
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            _ = parseResult;
            return RunAsync(standalone: false, cancellationToken);
        });

        return command;
    }

    private async Task<int> RunAsync(bool standalone, CancellationToken cancellationToken)
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
                LogValidationFailed(logger, errors.Count);
                return 2;
            }

            if (warnings.Count > 0 || hints.Count > 0)
            {
                ErrorPanels.ShowValidationReport(errors, warnings, hints);
            }

            if (standalone)
            {
                ShowSuccessPanel(hasNotes: warnings.Count > 0 || hints.Count > 0);
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
    private static partial void LogValidationFailed(ILogger logger, int errorCount);
}
