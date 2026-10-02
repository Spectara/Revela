namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Severity of a <see cref="ValidationDiagnostic"/> produced by an <see cref="ICheck"/>.
/// </summary>
/// <remarks>
/// Only <see cref="Error"/> changes the exit code of <c>revela check</c> (exit code 2).
/// <see cref="Warning"/> and <see cref="Hint"/> are only reported. No severity blocks
/// <c>generate</c>: checks run only when the user invokes <c>check</c>.
/// </remarks>
public enum ValidationSeverity
{
    /// <summary>A friendly note (e.g. a feature will be skipped).</summary>
    Hint,

    /// <summary>Something questionable but still buildable.</summary>
    Warning,

    /// <summary>A problem that prevents a correct build — <c>revela check</c> exits with code 2.</summary>
    Error,
}

/// <summary>
/// A single, human-readable finding from an <see cref="ICheck"/>.
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
    public static ValidationDiagnostic Error(string message, string? file = null, int? line = null, string? suggestion = null) =>
        new() { Severity = ValidationSeverity.Error, Message = message, File = file, Line = line, Suggestion = suggestion };

    /// <summary>Creates a warning diagnostic.</summary>
    public static ValidationDiagnostic Warning(string message, string? file = null, int? line = null, string? suggestion = null) =>
        new() { Severity = ValidationSeverity.Warning, Message = message, File = file, Line = line, Suggestion = suggestion };

    /// <summary>Creates a hint diagnostic.</summary>
    public static ValidationDiagnostic Hint(string message, string? file = null, int? line = null, string? suggestion = null) =>
        new() { Severity = ValidationSeverity.Hint, Message = message, File = file, Line = line, Suggestion = suggestion };
}
