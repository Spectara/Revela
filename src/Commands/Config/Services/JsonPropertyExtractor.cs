using System.Text.Json;
using System.Text.Json.Nodes;

using Spectara.Revela.Sdk.Json;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Extracts property paths and values from JSON documents and builds the edited document.
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
internal static class JsonPropertyExtractor
{
    /// <summary>
    /// Extracts all leaf properties from a JSON document.
    /// </summary>
    /// <param name="json">The JSON content to parse (comments and trailing commas allowed).</param>
    /// <returns>A list of (path, value) tuples for all scalar leaf properties.</returns>
    /// <exception cref="JsonException">Thrown if the JSON is invalid.</exception>
    public static IReadOnlyList<JsonProperty> ExtractProperties(string json)
    {
        using var document = JsonDocument.Parse(json, RevelaJsonOptions.LenientDocument);
        var properties = new List<JsonProperty>();
        ExtractPropertiesRecursive(document.RootElement, "", properties);
        return properties;
    }

    /// <summary>
    /// Builds the document to save: the template's structure merged with the existing document.
    /// </summary>
    /// <remarks>
    /// Template keys come first, in template order; every key of the existing document is kept
    /// (values the template doesn't know, e.g. <c>language</c>, are appended rather than dropped)
    /// and existing values win over template placeholders. Prompted values then replace string
    /// leaves, and fill <c>null</c> leaves when not empty; numbers, booleans and arrays keep their value.
    /// </remarks>
    /// <param name="templateJson">The theme's site template.</param>
    /// <param name="existingJson">The current file content, or <see langword="null"/> when creating.</param>
    /// <param name="values">The prompted values, keyed by dot-notation path.</param>
    /// <returns>The merged document.</returns>
    /// <exception cref="JsonException">Either document is invalid or not a JSON object.</exception>
    public static JsonObject BuildJson(
        string templateJson,
        string? existingJson,
        IReadOnlyDictionary<string, string> values)
    {
        var result = ParseObject(templateJson);
        if (existingJson is not null)
        {
            Overlay(result, ParseObject(existingJson));
        }

        ApplyValues(result, "", values);
        return result;
    }

    private static JsonObject ParseObject(string json) =>
        JsonNode.Parse(json, nodeOptions: null, RevelaJsonOptions.LenientDocument) as JsonObject
            ?? throw new JsonException("Expected a JSON object.");

    private static void Overlay(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (value is JsonObject sourceObject && target[key] is JsonObject targetObject)
            {
                Overlay(targetObject, sourceObject);
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }

    private static void ApplyValues(JsonObject node, string currentPath, IReadOnlyDictionary<string, string> values)
    {
        foreach (var key in node.Select(property => property.Key).ToList())
        {
            var path = string.IsNullOrEmpty(currentPath) ? key : $"{currentPath}.{key}";

            switch (node[key])
            {
                case JsonObject child:
                    ApplyValues(child, path, values);
                    break;

                case null when values.TryGetValue(path, out var value) && !string.IsNullOrEmpty(value):
                    node[key] = value;
                    break;

                case JsonValue leaf when leaf.GetValueKind() == JsonValueKind.String && values.TryGetValue(path, out var value):
                    node[key] = value;
                    break;

                default:
                    break;
            }
        }
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
}

/// <summary>
/// Represents a JSON property with its path and value.
/// </summary>
/// <param name="Path">The dot-notation path (e.g., "social.twitter").</param>
/// <param name="Value">The current value (empty string if not set).</param>
internal readonly record struct JsonProperty(string Path, string Value);
