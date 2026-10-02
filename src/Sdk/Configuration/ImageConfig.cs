using System.ComponentModel.DataAnnotations;

namespace Spectara.Revela.Sdk.Configuration;

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
    public int Webp { get; set; }

    /// <summary>
    /// JPEG quality (1-100). Set to 0 to disable JPEG output.
    /// </summary>
    /// <remarks>
    /// JPEG is the universal fallback format for older browsers.
    /// Recommended quality: 85-95. Set to 0 to disable.
    /// Default is 0 - user must explicitly configure via 'revela config image'.
    /// </remarks>
    public int Jpg { get; set; }

    /// <summary>
    /// AVIF quality (1-100). Set to 0 to disable AVIF output.
    /// </summary>
    /// <remarks>
    /// AVIF gives the smallest files: at quality 75 about 18% smaller than WebP 85 at visually
    /// equal quality on real photos (AVIF 80 was larger than WebP 85). Recommended quality: 70-80,
    /// the setup wizard uses 75. Set to 0 to disable.
    /// Default is 0 - user must explicitly configure via 'revela config image'.
    /// </remarks>
    public int Avif { get; set; }

    /// <summary>
    /// Default of <see cref="AvifEffort"/>.
    /// </summary>
    /// <remarks>
    /// Below libvips' own default (4): on detailed 25–33 MP photos effort 2 encoded about 7×
    /// faster with no visible difference and files no larger (about 2% smaller at equal quality).
    /// </remarks>
    public const int DefaultAvifEffort = 2;

    /// <summary>
    /// Default of <see cref="WebpEffort"/>, libvips' WebP encoder default.
    /// </summary>
    public const int DefaultWebpEffort = 4;

    /// <summary>
    /// AVIF encoder effort (0-9): CPU time spent to make each AVIF file smaller.
    /// </summary>
    /// <remarks>
    /// Default 2. libvips' own default 4 encodes about 7× slower without making files smaller
    /// on real photos; set it to keep AVIF files encoded before effort 2 became the default.
    /// Changing it re-encodes the AVIF variants only.
    /// </remarks>
    [Range(0, 9)]
    public int AvifEffort { get; set; } = DefaultAvifEffort;

    /// <summary>
    /// WebP encoder effort (0-6): CPU time spent to make each WebP file smaller.
    /// </summary>
    /// <remarks>
    /// Default 4 (libvips' default). Changing it re-encodes the WebP variants only.
    /// </remarks>
    [Range(0, 6)]
    public int WebpEffort { get; set; } = DefaultWebpEffort;

    /// <summary>
    /// Largest variant size in pixels, measured like the theme's sizes (by default the longest
    /// edge). 0 (default) keeps the photo's full resolution as the largest variant.
    /// </summary>
    /// <remarks>
    /// Each photo gets the theme sizes plus its full resolution for zooming. The full-resolution
    /// variant is the slowest to encode (about half of all AVIF time) and the largest file. With
    /// a cap, photos larger than it get the cap instead (e.g. 3840 for 4K displays: about 24%
    /// less CPU per photo, but zooming stops at the cap's resolution). Changing it re-encodes the
    /// photos larger than the old or the new cap.
    /// </remarks>
    [Range(0, int.MaxValue)]
    public int MaxSize { get; set; }

    /// <summary>
    /// Optional maximum number of images processed in parallel.
    /// </summary>
    /// <remarks>
    /// When null, Revela chooses: without AVIF about one image per CPU core (half as many from 8
    /// logical processors up, each with two libvips threads), never more than there are images to
    /// encode; with AVIF half the cores as images with up to 8 libvips threads each, which suits
    /// the AV1 encoder. Either way at most one image per GiB of memory. A configured value always
    /// wins; without AVIF the cores are then split between its images (up to 8 libvips threads
    /// each). Set to 1 to process images one at a time on low-memory systems.
    /// </remarks>
    public int? MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// Minimum image width in pixels. Images narrower than this are skipped during scan.
    /// </summary>
    /// <remarks>
    /// Useful for filtering out preview/thumbnail files that some programs or phones
    /// place alongside the actual photos. Set to 0 to disable filtering (default).
    /// </remarks>
    public int MinWidth { get; set; }

    /// <summary>
    /// Minimum image height in pixels. Images shorter than this are skipped during scan.
    /// </summary>
    /// <remarks>
    /// Useful for filtering out preview/thumbnail files. Combined with <see cref="MinWidth"/>,
    /// images failing either threshold are skipped. Set to 0 to disable filtering (default).
    /// </remarks>
    public int MinHeight { get; set; }

    /// <summary>
    /// Placeholder image settings for lazy loading
    /// </summary>
    /// <remarks>
    /// Configures placeholder generation using CSS-only LQIP technique.
    /// Default strategy is <see cref="PlaceholderStrategy.CssHash"/> - a compact integer (~7 bytes).
    /// Set to <c>None</c> to disable placeholders.
    /// </remarks>
    public PlaceholderConfig Placeholder { get; set; } = new();

    /// <summary>
    /// Gets the active formats (quality > 0) as a dictionary.
    /// </summary>
    /// <returns>Dictionary of format name to quality.</returns>
    /// <remarks>
    /// This is intentionally a method, not a property, because it creates a new
    /// dictionary on each call based on the current quality values.
    /// </remarks>
#pragma warning disable CA1024 // Method, not property: allocates a new Dictionary on every call (filtering Avif/Webp/Jpg by quality > 0). A property would mislead callers into expecting cheap, cached access.
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
