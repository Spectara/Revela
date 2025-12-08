using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Spectara.Revela.Commands.Generate.Models.Manifest;

/// <summary>
/// Manifest entry for a gallery (serializable subset of Gallery).
/// </summary>
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Required for JSON deserialization")]
public sealed class GalleryManifestEntry
{
    /// <summary>
    /// Original filesystem path relative to source directory.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// URL-safe slug for output.
    /// </summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    /// <summary>
    /// Gallery display name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Optional custom title from front matter.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Optional description from front matter.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Optional cover image filename.
    /// </summary>
    [JsonPropertyName("cover")]
    public string? Cover { get; init; }

    /// <summary>
    /// Gallery date for sorting.
    /// </summary>
    [JsonPropertyName("date")]
    public DateTime? Date { get; init; }

    /// <summary>
    /// Whether this gallery is featured.
    /// </summary>
    [JsonPropertyName("featured")]
    public bool Featured { get; init; }

    /// <summary>
    /// Manual sort weight.
    /// </summary>
    [JsonPropertyName("weight")]
    public int Weight { get; init; }

    /// <summary>
    /// Image source paths belonging to this gallery.
    /// </summary>
    [JsonPropertyName("imagePaths")]
    public List<string> ImagePaths { get; init; } = [];

    /// <summary>
    /// Nested sub-gallery entries.
    /// </summary>
    [JsonPropertyName("subGalleries")]
    public List<GalleryManifestEntry> SubGalleries { get; init; } = [];
}
