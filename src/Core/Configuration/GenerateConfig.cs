namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Generate output configuration
/// </summary>
/// <remarks>
/// <para>
/// Loaded from the "generate" section of revela.json or project.json.
/// Controls image processing, sorting, and camera mappings.
/// </para>
/// <para>
/// Note: Source and output paths are configured in the "paths" section
/// (see <see cref="Sdk.Configuration.PathsConfig"/>).
/// </para>
/// <para>
/// Global defaults in revela.json can be overridden per-project.
/// </para>
/// <para>
/// Note: Image sizes are defined by the theme (see <see cref="ThemeConfig"/>),
/// not in the generate section. Only format quality is configured here.
/// </para>
/// <example>
/// <code>
/// // project.json
/// {
///   "generate": {
///     "sorting": {
///       "galleries": "desc",
///       "images": {
///         "field": "dateTaken",
///         "direction": "desc",
///         "fallback": "filename"
///       }
///     },
///     "images": {
///       "webp": 85,
///       "jpg": 90,
///       "avif": 0
///     },
///     "cameras": {
///       "models": { "ILCE-7M4": "α 7 IV" }
///     }
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class GenerateConfig
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "generate";

    /// <summary>
    /// Sorting settings for galleries and images
    /// </summary>
    public SortingConfig Sorting { get; init; } = new();

    /// <summary>
    /// Image processing settings
    /// </summary>
    public ImageConfig Images { get; init; } = new();

    /// <summary>
    /// Rendering settings
    /// </summary>
    public RenderConfig Render { get; init; } = new();

    /// <summary>
    /// Camera model transformation settings
    /// </summary>
    public CameraConfig Cameras { get; init; } = new();
}

/// <summary>
/// Rendering configuration
/// </summary>
public sealed class RenderConfig
{
    /// <summary>
    /// Enable parallel rendering of galleries/pages.
    /// </summary>
    /// <remarks>
    /// Default is false; set to true to speed up rendering on multi-core machines.
    /// </remarks>
    public bool Parallel { get; init; }

    /// <summary>
    /// Optional maximum degree of parallelism.
    /// </summary>
    /// <remarks>
    /// When null, uses the default from ParallelOptions (Environment.ProcessorCount).
    /// </remarks>
    public int? MaxDegreeOfParallelism { get; init; }
}

/// <summary>
/// Sorting configuration for galleries and images
/// </summary>
/// <remarks>
/// <para>
/// Controls the sort order of galleries in navigation and images within galleries.
/// Images can be sorted by any property path including EXIF data.
/// </para>
/// <example>
/// <code>
/// // project.json
/// {
///   "generate": {
///     "sorting": {
///       "galleries": "desc",
///       "images": {
///         "field": "dateTaken",
///         "direction": "desc",
///         "fallback": "filename"
///       }
///     }
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class SortingConfig
{
    /// <summary>
    /// Gallery sort direction (always sorted by name with natural ordering).
    /// </summary>
    /// <remarks>
    /// Galleries are always sorted by folder name using natural ordering
    /// (1, 2, 10 instead of 1, 10, 2). Sort prefixes like "01 Events" are
    /// stripped for display but used for ordering.
    /// </remarks>
    public SortDirection Galleries { get; init; } = SortDirection.Asc;

    /// <summary>
    /// Image sort configuration within galleries.
    /// </summary>
    /// <remarks>
    /// Can be overridden per gallery using front matter:
    /// <code>
    /// +++
    /// sort = "exif.rating"
    /// sort_direction = "desc"
    /// +++
    /// </code>
    /// </remarks>
    public ImageSortConfig Images { get; init; } = new();
}

/// <summary>
/// Sort direction
/// </summary>
/// <remarks>
/// JSON values: "asc" or "desc" (case-insensitive).
/// </remarks>
public enum SortDirection
{
    /// <summary>A → Z, 1 → 9, oldest → newest</summary>
    Asc,

    /// <summary>Z → A, 9 → 1, newest → oldest</summary>
    Desc
}

/// <summary>
/// Image sort configuration
/// </summary>
/// <remarks>
/// <para>
/// Allows flexible sorting by any property path. Supports:
/// </para>
/// <list type="bullet">
///   <item><c>filename</c> - File name</item>
///   <item><c>dateTaken</c> - EXIF date taken</item>
///   <item><c>exif.focalLength</c> - Focal length</item>
///   <item><c>exif.iso</c> - ISO sensitivity</item>
///   <item><c>exif.aperture</c> - Aperture (f-number)</item>
///   <item><c>exif.raw.Rating</c> - Star rating (1-5)</item>
///   <item><c>exif.raw.{FieldName}</c> - Any field from EXIF Raw dictionary</item>
/// </list>
/// <para>
/// When the primary field is null/missing, falls back to the fallback field.
/// </para>
/// </remarks>
public sealed class ImageSortConfig
{
    /// <summary>
    /// Property path to sort by.
    /// </summary>
    /// <remarks>
    /// Use dot notation for nested properties: "exif.focalLength", "exif.raw.Rating".
    /// </remarks>
    public string Field { get; init; } = "dateTaken";

    /// <summary>
    /// Sort direction.
    /// </summary>
    public SortDirection Direction { get; init; } = SortDirection.Desc;

    /// <summary>
    /// Fallback field when primary field is null/missing.
    /// </summary>
    /// <remarks>
    /// Uses same direction as primary sort.
    /// </remarks>
    public string Fallback { get; init; } = "filename";
}

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

/// <summary>
/// Camera model transformation settings
/// </summary>
/// <remarks>
/// Custom mappings override built-in defaults for Sony ILCE → α series.
/// </remarks>
public sealed class CameraConfig
{
    /// <summary>
    /// Custom camera model mappings (e.g., "ILCE-7M4" → "α 7 IV").
    /// Merged with built-in defaults (custom values override defaults).
    /// </summary>
    public Dictionary<string, string> Models { get; init; } = [];

    /// <summary>
    /// Custom manufacturer name mappings (e.g., "SONY" → "Sony").
    /// Merged with built-in defaults (custom values override defaults).
    /// </summary>
    public Dictionary<string, string> Makes { get; init; } = [];
}
