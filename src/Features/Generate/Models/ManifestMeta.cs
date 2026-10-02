using System.Text.Json.Serialization;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Manifest metadata for tracking configuration changes.
/// </summary>
/// <remarks>
/// Immutable: all properties are <c>init</c>-only. To update metadata, use the
/// <c>with</c> expression to produce a modified copy.
/// </remarks>
internal sealed record ManifestMeta
{
    /// <summary>
    /// Manifest schema version.
    /// </summary>
    /// <remarks>
    /// Version history:
    /// <list type="bullet">
    ///   <item><description>v1: Initial version with separate galleries and images</description></item>
    ///   <item><description>v2: Added navigation tree</description></item>
    ///   <item><description>v3: Unified tree structure with root node containing everything</description></item>
    ///   <item><description>v4: Polymorphic content list (images + markdown), renamed images to content</description></item>
    ///   <item><description>v5: Content holds only images (no type discriminator); removed configHash and unused entry fields</description></item>
    /// </list>
    /// A manifest with another version is discarded on load and rebuilt by the next scan.
    /// <para>
    /// Image processing state is not part of the manifest: it lives in <c>.revela/state/images.json</c>
    /// with its own version (<see cref="Services.ImageStateStore"/>), so discarding the manifest
    /// never re-encodes images. Version 5 manifests written by earlier builds (in <c>.cache</c>) still
    /// carry <c>processedImages</c> and <c>formatQualities</c>; they are carried over once
    /// (<see cref="Services.LegacyCacheCarryOver"/>) and otherwise ignored, which is why their
    /// removal did not need a new version.
    /// </para>
    /// </remarks>
    [JsonPropertyName("version")]
    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    /// The manifest schema version this Revela reads and writes.
    /// </summary>
    public const int CurrentVersion = 5;

    /// <summary>
    /// Hash of scan configuration (placeholder strategy, min dimensions).
    /// When this changes, all metadata needs to be re-read from source files.
    /// </summary>
    [JsonPropertyName("scanConfigHash")]
    public string ScanConfigHash { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp of last content scan.
    /// </summary>
    [JsonPropertyName("lastScanned")]
    public DateTime? LastScanned { get; init; }

    /// <summary>
    /// Timestamp of last image processing.
    /// </summary>
    [JsonPropertyName("lastImagesProcessed")]
    public DateTime? LastImagesProcessed { get; init; }

    /// <summary>
    /// Timestamp of last manifest update (any change).
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}
