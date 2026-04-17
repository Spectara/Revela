using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Configuration;

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
/// (see <see cref="PathsConfig"/>).
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
[RevelaConfig("generate", ValidateDataAnnotations = false, ValidateOnStart = false)]
public sealed class GenerateConfig
{
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
