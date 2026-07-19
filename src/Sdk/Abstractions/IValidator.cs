namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Contributes cheap, structural checks to <c>revela check</c> / Phase 0 of
/// <c>generate all</c>, letting a plugin validate its own <c>generate</c> preconditions.
/// </summary>
/// <remarks>
/// <para>
/// A plugin implements this when it participates during <c>generate</c> and can detect a
/// problem up front that would otherwise surface — as a failure or broken output — once the
/// pipeline runs. The host collects every registered validator alongside its own structural
/// checks and reports all findings in a single pass (collect-all); any <see cref="ValidationSeverity.Error"/>
/// blocks the build (exit code 2), while warnings and hints are surfaced but never abort it.
/// </para>
/// <para>
/// Implementations must be fast and structural only: no network access and no expensive I/O
/// (no image decoding, no downloads). They validate <c>generate</c> preconditions exclusively —
/// not the preconditions of <c>sync</c>, <c>fetch</c>, or any other lifecycle phase. A share URL
/// or a remote feed being reachable is <em>not</em> a generate concern and must not be checked here.
/// </para>
/// <para>
/// Context is obtained through constructor injection (the plugin's own
/// <c>IOptionsMonitor&lt;TConfig&gt;</c>, <c>IPathResolver</c>, <c>ILogger&lt;T&gt;</c>) rather than
/// a shared context parameter. Register with
/// <c>services.TryAddEnumerable(ServiceDescriptor.Transient&lt;IValidator, TValidator&gt;())</c>.
/// </para>
/// </remarks>
public interface IValidator
{
    /// <summary>
    /// Runs this plugin's structural checks and returns every finding in a single pass.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All diagnostics found; empty when there is nothing to report.</returns>
    ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default);
}
