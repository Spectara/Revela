namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Image processing configuration
/// </summary>
/// <remarks>
/// <para>
/// Controls image output formats and quality settings.
/// Sizes are defined by the theme (see <see cref="ThemeImagesConfig"/>).
/// </para>
/// <para>
/// Format quality is 1-100. Set to 0 to disable a format.
/// </para>
/// <example>
/// <code>
/// // project.json - only webp, no jpg
/// {
///   "generate": {
///     "images": {
///       "webp": 85,
///       "jpg": 0,
///       "avif": 0
///     }
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class ImageConfig
{
    /// <summary>
    /// WebP quality (1-100). Set to 0 to disable WebP output.
    /// </summary>
    /// <remarks>
    /// WebP offers good compression with wide browser support.
    /// Recommended quality: 80-90. Set to 0 to disable.
    /// Default is 0 - user must explicitly configure via 'revela config image'.
    /// </remarks>
    public int Webp { get; init; }

    /// <summary>
    /// JPEG quality (1-100). Set to 0 to disable JPEG output.
    /// </summary>
    /// <remarks>
    /// JPEG is the universal fallback format for older browsers.
    /// Recommended quality: 85-95. Set to 0 to disable.
    /// Default is 0 - user must explicitly configure via 'revela config image'.
    /// </remarks>
    public int Jpg { get; init; }

    /// <summary>
    /// AVIF quality (1-100). Set to 0 to disable AVIF output.
    /// </summary>
    /// <remarks>
    /// AVIF offers best compression but encoding is ~10x slower than WebP.
    /// Recommended quality: 75-85. Set to 0 to disable.
    /// Default is 0 - user must explicitly configure via 'revela config image'.
    /// </remarks>
    public int Avif { get; init; }

    /// <summary>
    /// Optional maximum degree of parallelism for image processing.
    /// </summary>
    /// <remarks>
    /// When null, defaults to <c>Environment.ProcessorCount - 2</c> to leave headroom.
    /// Set to 1 to process images sequentially on low-memory systems.
    /// </remarks>
    public int? MaxDegreeOfParallelism { get; init; }

    /// <summary>
    /// Minimum image width in pixels. Images narrower than this are skipped during scan.
    /// </summary>
    /// <remarks>
    /// Useful for filtering out preview/thumbnail files that some programs or phones
    /// place alongside the actual photos. Set to 0 to disable filtering (default).
    /// </remarks>
    public int MinWidth { get; init; }

    /// <summary>
    /// Minimum image height in pixels. Images shorter than this are skipped during scan.
    /// </summary>
    /// <remarks>
    /// Useful for filtering out preview/thumbnail files. Combined with <see cref="MinWidth"/>,
    /// images failing either threshold are skipped. Set to 0 to disable filtering (default).
    /// </remarks>
    public int MinHeight { get; init; }

    /// <summary>
    /// Placeholder image settings for lazy loading
    /// </summary>
    /// <remarks>
    /// Configures placeholder generation using CSS-only LQIP technique.
    /// Default strategy is <see cref="PlaceholderStrategy.CssHash"/> - a compact integer (~7 bytes).
    /// Set to <c>None</c> to disable placeholders.
    /// </remarks>
    public PlaceholderConfig Placeholder { get; init; } = new();

    /// <summary>
    /// Gets the active formats (quality > 0) as a dictionary.
    /// </summary>
    /// <returns>Dictionary of format name to quality.</returns>
    /// <remarks>
    /// This is intentionally a method, not a property, because it creates a new
    /// dictionary on each call based on the current quality values.
    /// </remarks>
#pragma warning disable CA1024 // Use properties where appropriate
    public IReadOnlyDictionary<string, int> GetActiveFormats()
#pragma warning restore CA1024
    {
        var formats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (Avif > 0)
        {
            formats["avif"] = Avif;
        }

        if (Webp > 0)
        {
            formats["webp"] = Webp;
        }

        if (Jpg > 0)
        {
            formats["jpg"] = Jpg;
        }

        return formats;
    }
}
