namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// A single, self-contained diagnostic unit of <c>revela check</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each check is the single source of truth for one aspect of a project's health
/// (configuration, structure, theme, content, gallery URLs, plugin preconditions, …).
/// The host surfaces every registered check in two ways from the same implementation:
/// its own <c>check &lt;name&gt;</c> sub-command (the host auto-wraps the check — plugins
/// do not hand-write a command) and the aggregate <c>check all</c> report.
/// </para>
/// <para>
/// Checks must be fast and structural only: no network access and no expensive I/O
/// (no image decoding, no downloads). They run collect-all — every problem is returned
/// in one pass rather than stopping at the first. Any <see cref="ValidationSeverity.Error"/>
/// blocks the build (exit code 2); warnings and hints are surfaced but never abort it.
/// </para>
/// <para>
/// Context is obtained through constructor injection (the check's own
/// <c>IOptionsMonitor&lt;TConfig&gt;</c>, <c>IPathResolver</c>, <c>ILogger&lt;T&gt;</c>).
/// Register with
/// <c>services.TryAddEnumerable(ServiceDescriptor.Transient&lt;ICheck, TCheck&gt;())</c>.
/// </para>
/// </remarks>
public interface ICheck
{
    /// <summary>
    /// Gets the short, stable identifier used as the <c>check &lt;name&gt;</c> sub-command
    /// name and in logging (e.g. <c>"config"</c>, <c>"structure"</c>, <c>"calendar"</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the human-readable title shown in the sub-command description
    /// (e.g. <c>"Configuration"</c>, <c>"Gallery URLs"</c>).
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Runs this check and returns every finding in a single pass.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All diagnostics found; empty when there is nothing to report.</returns>
    ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default);
}
