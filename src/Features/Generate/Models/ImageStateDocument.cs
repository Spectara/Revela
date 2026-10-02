using System.Text.Json.Serialization;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// File format of the image processing state (<c>.revela/state/images.json</c>).
/// </summary>
internal sealed record ImageStateDocument
{
    /// <summary>
    /// State schema version, independent of the manifest version.
    /// </summary>
    /// <remarks>
    /// A file with another version is ignored, which re-encodes every image once. Bump it only
    /// when the recorded values can no longer decide whether variants are current.
    /// </remarks>
    [JsonPropertyName("version")]
    public int Version { get; init; }

    /// <summary>
    /// Processed images by source path (forward slashes, relative to the source directory).
    /// </summary>
    [JsonPropertyName("images")]
    public IReadOnlyDictionary<string, ProcessedImage?>? Images { get; init; }
}
