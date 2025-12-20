using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Spectara.Revela.Commands.Config.Models;

/// <summary>
/// DTO for project.json file structure.
/// </summary>
/// <remarks>
/// This matches the actual JSON file structure, which differs from the
/// internal RevelaConfig hierarchy. Used for serialization/deserialization.
/// Properties use setters intentionally for JSON deserialization.
/// </remarks>
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "DTO for JSON file")]
public sealed class ProjectConfigDto
{
    /// <summary>
    /// Project name used for identification.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Base URL for the generated site.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Theme name to use.
    /// </summary>
    [JsonPropertyName("theme")]
    public string? Theme { get; set; }

    /// <summary>
    /// Plugin configuration.
    /// </summary>
    [JsonPropertyName("plugins")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO for JSON serialization")]
    public Dictionary<string, object>? Plugins { get; set; }

    /// <summary>
    /// Base path for image references (CDN URL or relative path).
    /// </summary>
    [JsonPropertyName("imageBasePath")]
    public string? ImageBasePath { get; set; }

    /// <summary>
    /// Base path for subdirectory hosting.
    /// </summary>
    [JsonPropertyName("basePath")]
    public string? BasePath { get; set; }

    /// <summary>
    /// Generate settings.
    /// </summary>
    [JsonPropertyName("generate")]
    public GenerateConfigDto? Generate { get; set; }
}

/// <summary>
/// DTO for generate section in project.json.
/// </summary>
public sealed class GenerateConfigDto
{
    /// <summary>
    /// Output directory path.
    /// </summary>
    [JsonPropertyName("output")]
    public string? Output { get; set; }

    /// <summary>
    /// Image processing settings.
    /// </summary>
    [JsonPropertyName("images")]
    public ImageConfigDto? Images { get; set; }
}

/// <summary>
/// DTO for image settings in project.json.
/// </summary>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO for JSON serialization")]
public sealed class ImageConfigDto
{
    /// <summary>
    /// Output formats with quality settings.
    /// Key = format (avif, webp, jpg), Value = quality (1-100).
    /// </summary>
    [JsonPropertyName("formats")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO for JSON serialization")]
    public Dictionary<string, int>? Formats { get; set; }

    /// <summary>
    /// Image widths to generate (in pixels).
    /// </summary>
    [JsonPropertyName("sizes")]
    public int[]? Sizes { get; set; }

    /// <summary>
    /// Minimum image width in pixels.
    /// </summary>
    [JsonPropertyName("minWidth")]
    public int? MinWidth { get; set; }

    /// <summary>
    /// Minimum image height in pixels.
    /// </summary>
    [JsonPropertyName("minHeight")]
    public int? MinHeight { get; set; }
}
