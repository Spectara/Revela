namespace Spectara.Revela.Features.Generate.Abstractions;

/// <summary>
/// Validates a Revela project's structure and configuration with cheap, structural
/// checks only — no image decoding and no network access.
/// </summary>
/// <remarks>
/// <para>
/// A single shared implementation backs two entry points: the standalone
/// <c>revela check</c> command and Phase 0 of <c>generate all</c> (which fails fast on
/// errors before the expensive image step). Warnings and hints are surfaced but never
/// abort the build.
/// </para>
/// <para>
/// Validation is collect-all: every problem is reported in one pass rather than stopping
/// at the first failure.
/// </para>
/// </remarks>
public interface ISiteValidator
{
    /// <summary>
    /// Runs all structural checks and returns every finding in a single pass.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All diagnostics found; empty when the project is structurally sound.</returns>
    ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default);
}
