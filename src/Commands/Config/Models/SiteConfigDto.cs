using System.Text.Json.Serialization;

namespace Spectara.Revela.Commands.Config.Models;

/// <summary>
/// DTO for site.json file structure.
/// </summary>
/// <remarks>
/// This matches the actual JSON file structure.
/// Used for serialization/deserialization.
/// </remarks>
public sealed class SiteConfigDto
{
    /// <summary>
    /// Site title displayed in browser and headers.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Author name for attribution.
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>
    /// Site description for SEO meta tags.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Copyright notice.
    /// </summary>
    [JsonPropertyName("copyright")]
    public string? Copyright { get; set; }
}
