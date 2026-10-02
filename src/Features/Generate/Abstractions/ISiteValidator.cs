using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Features.Generate.Abstractions;

/// <summary>
/// Validates a Revela project's structure and configuration with cheap, structural
/// checks only — no image decoding and no network access.
/// </summary>
/// <remarks>
/// <para>
/// Backs the <c>revela check</c> report, which exits with code 2 when any check reports
/// an error. <c>generate</c> does not run checks; its steps fail with their own clear
/// messages (e.g. a missing site.json title or theme layout).
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
