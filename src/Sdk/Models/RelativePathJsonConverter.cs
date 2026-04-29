using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectara.Revela.Sdk.Models;

/// <summary>
/// JSON converter that serializes <see cref="RelativePath"/> as a plain string
/// (e.g. <c>"events/fireworks"</c>) rather than as an object with a <c>value</c>
/// property.
/// </summary>
/// <remarks>
/// Without this converter, System.Text.Json would treat <see cref="RelativePath"/>
/// as a record struct with a single property and serialize it as
/// <c>{"value":"events/fireworks"}</c> — breaking JSON compatibility with the
/// previous <see cref="string"/>-typed properties on the manifest.
/// </remarks>
public sealed class RelativePathJsonConverter : JsonConverter<RelativePath>
{
    /// <inheritdoc />
    public override RelativePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value is null ? RelativePath.Empty : new RelativePath(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
