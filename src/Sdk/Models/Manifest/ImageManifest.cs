using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Site manifest for incremental builds and caching.
/// </summary>
/// <remarks>
/// <para>
/// The manifest stores metadata about all processed content in a unified tree structure.
/// The root node represents the home page and contains the entire site hierarchy.
/// </para>
/// <para>
/// Enables:
/// <list type="bullet">
///   <item><description>Skip unchanged images (hash comparison)</description></item>
///   <item><description>Provide gallery/navigation data without re-scanning</description></item>
///   <item><description>Dynamic srcset based on actually generated sizes</description></item>
///   <item><description>EXIF data caching (replaces separate ExifCache)</description></item>
/// </list>
/// </para>
/// <para>Location: <c>.cache/manifest.json</c></para>
/// <para>
/// <b>Immutable:</b> all properties are <c>init</c>-only. To update the manifest,
/// use the <c>with</c> expression to produce a modified copy.
/// </para>
/// </remarks>
public sealed record ImageManifest
{
    /// <summary>
    /// Manifest metadata for version and configuration tracking.
    /// </summary>
    [JsonPropertyName("_meta")]
    public ManifestMeta Meta { get; init; } = new();

    /// <summary>
    /// Root node of the site tree (home page).
    /// </summary>
    /// <remarks>
    /// The entire site structure is represented as a tree starting from this root.
    /// Galleries with images have a non-null slug, branch nodes have null slug.
    /// </remarks>
    [JsonPropertyName("root")]
    public ManifestEntry? Root { get; init; }
}
