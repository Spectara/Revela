using Spectara.Revela.Features.Generate.Models.Results;

namespace Spectara.Revela.Features.Generate.Abstractions;

/// <summary>
/// Renders the HTML pages of the site from the manifest and copies theme assets.
/// </summary>
internal interface IRenderService
{
    /// <summary>
    /// Render HTML pages from manifest data.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Render result with statistics.</returns>
    Task<RenderResult> RenderAsync(
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
