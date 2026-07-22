using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Features.Generate.Services.Checks;

/// <summary>
/// Implemented by host checks that need the scanned <see cref="ContentTree"/> so the
/// aggregate <c>check all</c> run can scan the source once and share the result across
/// every consumer instead of re-scanning per check.
/// </summary>
/// <remarks>
/// When a check is run on its own (<c>check &lt;name&gt;</c>) the host calls the plain
/// <see cref="ICheck.ValidateAsync"/>, which scans for itself. When the check runs as
/// part of <c>check all</c>, <see cref="CheckService"/> supplies the shared tree here.
/// </remarks>
internal interface IContentAwareCheck
{
    /// <summary>
    /// Runs the check against an already-scanned content tree.
    /// </summary>
    /// <param name="tree">The shared, pre-scanned content tree.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All diagnostics found; empty when there is nothing to report.</returns>
    ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(ContentTree tree, CancellationToken cancellationToken = default);
}
