using Spectara.Revela.Commands.Generate.Models.Results;

namespace Spectara.Revela.Commands.Generate.Abstractions;

/// <summary>
/// Service for content scanning and gallery/navigation building.
/// </summary>
/// <remarks>
/// <para>
/// Scans the source directory structure to:
/// </para>
/// <list type="bullet">
///   <item><description>Discover galleries and images</description></item>
///   <item><description>Parse front matter from _index.md files</description></item>
///   <item><description>Build navigation tree</description></item>
///   <item><description>Update manifest with gallery/navigation data</description></item>
/// </list>
/// </remarks>
public interface IContentService
{
    /// <summary>
    /// Scan content directory and update manifest with galleries/navigation.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Scan result with statistics.</returns>
    Task<ContentResult> ScanAsync(
        IProgress<ContentProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
