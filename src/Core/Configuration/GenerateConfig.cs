namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Generate output configuration
/// </summary>
/// <remarks>
/// <para>
/// Loaded from the "generate" section of revela.json or project.json.
/// Controls output directory, image processing, and camera mappings.
/// </para>
/// <para>
/// Global defaults in revela.json can be overridden per-project.
/// </para>
/// <example>
/// <code>
/// // project.json
/// {
///   "generate": {
///     "output": "dist",
///     "images": {
///       "formats": { "webp": 85, "jpg": 90 },
///       "sizes": [640, 1024, 1920, 2560]
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
    /// Output directory path (relative to project root)
    /// </summary>
    public string Output { get; init; } = "output";

    /// <summary>
    /// Image processing settings
    /// </summary>
    public ImageConfig Images { get; init; } = new();

    /// <summary>
    /// Camera model transformation settings
    /// </summary>
    public CameraConfig Cameras { get; init; } = new();
}

/// <summary>
/// Image processing configuration
/// </summary>
public sealed class ImageConfig
{
    /// <summary>
    /// Default output formats with quality settings.
    /// Used when no formats are configured in project.json.
    /// </summary>
    /// <remarks>
    /// AVIF is not included by default due to very slow encoding (~10x slower than WebP).
    /// Users can enable it manually in project.json for better compression.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, int> DefaultFormats = new Dictionary<string, int>
    {
        ["webp"] = 85,
        ["jpg"] = 90
    };

    /// <summary>
    /// Output formats with quality settings.
    /// Key = format (avif, webp, jpg), Value = quality (1-100).
    /// </summary>
    /// <remarks>
    /// Empty by default - consumers should use <see cref="DefaultFormats"/> as fallback.
    /// This allows project.json to completely replace formats instead of merging.
    /// AVIF can be added manually for better compression (but ~10x slower encoding).
    /// </remarks>
    /// <example>
    /// { "webp": 85, "jpg": 90 }
    /// </example>
    public IReadOnlyDictionary<string, int> Formats { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Image widths to generate (in pixels)
    /// </summary>
    public IReadOnlyList<int> Sizes { get; init; } = [640, 1024, 1280, 1920, 2560];

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
