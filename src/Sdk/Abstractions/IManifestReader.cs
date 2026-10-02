using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Read-only access to the site manifest written by the last <c>generate scan</c>.
/// </summary>
/// <remarks>
/// <para>
/// Plugins that derive data from scanned content (statistics, calendar pages, …)
/// inject this instead of locating <c>.revela/core/manifest.json</c> themselves. Writing
/// the manifest is reserved for the host's scan and image steps.
/// </para>
/// <para>
/// Each call reads the manifest from disk, so a pipeline step always sees the
/// result of the scan that ran before it. Reading never creates files.
/// </para>
/// </remarks>
public interface IManifestReader
{
    /// <summary>
    /// Loads the manifest written by the last scan.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The scanned site, or <c>null</c> when there is no usable manifest — the project
    /// has not been scanned yet, or the file is unreadable or from an older Revela
    /// version. Report that <c>revela generate scan</c> must run first.
    /// </returns>
    Task<ManifestSnapshot?> TryLoadAsync(CancellationToken cancellationToken = default);
}
