using System.Text.Json;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Extracts property paths and values from JSON documents.
/// </summary>
/// <remarks>
/// <para>
/// Used to dynamically generate CLI prompts from JSON structure.
/// Walks the JSON tree and extracts all leaf properties with their dot-notation paths.
/// </para>
/// <para>
/// Example: { "title": "My Site", "social": { "twitter": "@me" } }
/// Returns: [("title", "My Site"), ("social.twitter", "@me")]
/// </para>
/// </remarks>
public static class JsonPropertyExtractor
{
    /// <summary>
    /// Extracts all leaf properties from a JSON document.
    /// </summary>
    /// <param name="json">The JSON content to parse.</param>
    /// <returns>A list of (path, value) tuples for all string leaf properties.</returns>
    /// <exception cref="JsonException">Thrown if the JSON is invalid.</exception>
    public static IReadOnlyList<JsonProperty> ExtractProperties(string json)
    {
        using var document = JsonDocument.Parse(json);
        var properties = new List<JsonProperty>();
        ExtractPropertiesRecursive(document.RootElement, "", properties);
        return properties;
    }

    /// <summary>
    /// Builds a JSON document from property values.
    /// </summary>
    /// <param name="templateJson">The template JSON to use as structure.</param>
    /// <param name="values">The values to set, keyed by dot-notation path.</param>
    /// <returns>The JSON string with values filled in.</returns>
    public static string BuildJson(string templateJson, IReadOnlyDictionary<string, string> values)
    {
        using var document = JsonDocument.Parse(templateJson);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        WriteElement(writer, document.RootElement, "", values);

        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void ExtractPropertiesRecursive(
        JsonElement element,
        string currentPath,
        List<JsonProperty> properties)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var path = string.IsNullOrEmpty(currentPath)
                        ? property.Name
                        : $"{currentPath}.{property.Name}";

                    ExtractPropertiesRecursive(property.Value, path, properties);
                }

                break;

            case JsonValueKind.String:
                properties.Add(new JsonProperty(currentPath, element.GetString() ?? ""));
                break;

            case JsonValueKind.Number:
                properties.Add(new JsonProperty(currentPath, element.GetRawText()));
                break;

            case JsonValueKind.True:
                properties.Add(new JsonProperty(currentPath, "true"));
                break;

            case JsonValueKind.False:
                properties.Add(new JsonProperty(currentPath, "false"));
                break;

            case JsonValueKind.Array:
                // Skip arrays for now - not typically used in site.json
                break;

            case JsonValueKind.Null:
                properties.Add(new JsonProperty(currentPath, ""));
                break;

            case JsonValueKind.Undefined:
            default:
                // Skip undefined or unknown value kinds
                break;
        }
    }

    private static void WriteElement(
        Utf8JsonWriter writer,
        JsonElement element,
        string currentPath,
        IReadOnlyDictionary<string, string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    var path = string.IsNullOrEmpty(currentPath)
                        ? property.Name
                        : $"{currentPath}.{property.Name}";

                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value, path, values);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.String:
                // Use value from dictionary if available, otherwise keep original
                var stringValue = values.TryGetValue(currentPath, out var newValue)
                    ? newValue
                    : element.GetString() ?? "";
                writer.WriteStringValue(stringValue);
                break;

            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                element.WriteTo(writer);
                break;

            case JsonValueKind.Array:
                element.WriteTo(writer);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            case JsonValueKind.Undefined:
            default:
                // Skip undefined or unknown value kinds
                break;
        }
    }
}

/// <summary>
/// Represents a JSON property with its path and value.
/// </summary>
/// <param name="Path">The dot-notation path (e.g., "social.twitter").</param>
/// <param name="Value">The current value (empty string if not set).</param>
public readonly record struct JsonProperty(string Path, string Value);
