using System.Text.Json.Serialization;

namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// What an image's variants were generated from, recorded after they were written successfully.
/// </summary>
internal sealed record ProcessedImage
{
    /// <summary>
    /// Fingerprint of the source file and the pipeline settings that apply to every variant
    /// (output version, resize mode).
    /// </summary>
    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Quality per format that the image's variants on disk were encoded with.
    /// </summary>
    /// <remarks>
    /// Recorded per image (not once per run) so a run interrupted halfway through a quality
    /// change only re-encodes the images it had not reached yet.
    /// </remarks>
    [JsonPropertyName("qualities")]
    public IReadOnlyDictionary<string, int> Qualities { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Encoder effort per format that differs from libvips' default, as encoded on disk.
    /// </summary>
    /// <remarks>
    /// Absent for formats encoded with the default effort, and omitted entirely when every format
    /// uses it, so the state of a default configuration stays as it was before effort was
    /// configurable. Like <see cref="Qualities"/>, a change re-encodes only that format.
    /// </remarks>
    [JsonPropertyName("efforts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? Efforts { get; init; }
}
