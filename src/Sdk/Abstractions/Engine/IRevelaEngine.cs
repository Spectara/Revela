using Spectara.Revela.Sdk.Models.Engine;

namespace Spectara.Revela.Sdk.Abstractions.Engine;

/// <summary>
/// High-level API for triggering Revela operations.
/// </summary>
/// <remarks>
/// <para>
/// This is the primary interface for plugins that need to invoke generation
/// operations. It provides a stable, high-level facade over the internal
/// generation pipeline.
/// </para>
/// <para>
/// Used by:
/// <list type="bullet">
/// <item>MCP Server plugin — to trigger generation via AI assistants</item>
/// <item>GUI plugin — to trigger generation from a web interface</item>
/// <item>Third-party plugins — e.g., auto-regenerate after upload</item>
/// </list>
/// </para>
/// <para>
/// All methods accept an optional <see cref="IProgress{T}"/> for reporting progress
/// back to the caller, enabling different UIs (console, JSON, data binding) to
/// display progress in their own way.
/// </para>
/// </remarks>
public interface IRevelaEngine
{
    /// <summary>
    /// Scans the source directory and builds the content manifest.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Scan results including gallery and image counts.</returns>
    Task<ScanResult> ScanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders HTML pages from the content manifest.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendering results including page count.</returns>
    Task<PagesResult> GeneratePagesAsync(
        IProgress<PagesProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes and resizes images from the content manifest.
    /// </summary>
    /// <param name="force">Force rebuild all images, ignoring cache.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing results including file counts and sizes.</returns>
    Task<ImagesResult> GenerateImagesAsync(
        bool force = false,
        IProgress<ImagesProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the full generation pipeline (scan → pages → images → plugin steps).
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined results from all pipeline phases.</returns>
    Task<GenerateResult> GenerateAllAsync(
        IProgress<GenerateProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
