namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Severity of a <see cref="ValidationDiagnostic"/> produced by a validator.
/// </summary>
/// <remarks>
/// Only <see cref="Error"/> blocks a build (exit code 2). <see cref="Warning"/> and
/// <see cref="Hint"/> are surfaced but never abort <c>generate all</c>.
/// </remarks>
public enum ValidationSeverity
{
    /// <summary>A friendly, non-blocking note (e.g. a feature will be skipped).</summary>
    Hint,

    /// <summary>Something questionable but still buildable — surfaced, does not block.</summary>
    Warning,

    /// <summary>A problem that prevents a correct build — blocks with exit code 2.</summary>
    Error,
}

/// <summary>
/// A single, human-readable finding from site validation.
/// </summary>
/// <remarks>
/// Diagnostics are collected in a single pass (collect-all) so the user sees every
/// problem at once instead of fixing them one round-trip at a time.
/// </remarks>
public sealed record ValidationDiagnostic
{
    /// <summary>How serious the finding is.</summary>
    public required ValidationSeverity Severity { get; init; }

    /// <summary>Plain-language description of the finding (no markup).</summary>
    public required string Message { get; init; }

    /// <summary>Optional path to the file the finding relates to (relative or absolute).</summary>
    public string? File { get; init; }

    /// <summary>Optional 1-based line number within <see cref="File"/>.</summary>
    public int? Line { get; init; }

    /// <summary>Optional short suggestion for how to resolve the finding.</summary>
    public string? Suggestion { get; init; }

    /// <summary>Creates an error diagnostic.</summary>
    public static ValidationDiagnostic Error(string message, string? file = null, int? line = null, string? hint = null) =>
        new() { Severity = ValidationSeverity.Error, Message = message, File = file, Line = line, Suggestion = hint };

    /// <summary>Creates a warning diagnostic.</summary>
    public static ValidationDiagnostic Warning(string message, string? file = null, int? line = null, string? hint = null) =>
        new() { Severity = ValidationSeverity.Warning, Message = message, File = file, Line = line, Suggestion = hint };

    /// <summary>Creates a hint diagnostic.</summary>
    public static ValidationDiagnostic Hint(string message, string? file = null, int? line = null, string? hint = null) =>
        new() { Severity = ValidationSeverity.Hint, Message = message, File = file, Line = line, Suggestion = hint };
}
