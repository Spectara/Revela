using System.Text.Json;
using Scriban.Runtime;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Converts JSON (<c>site.json</c>, plugin data files) into Scriban-native values.
/// </summary>
/// <remarks>
/// Templates read the result with dot notation (<c>site.title</c>, <c>statistics.cameras</c>)
/// without reflection: objects become <see cref="ScriptObject"/>, arrays <see cref="ScriptArray"/>,
/// integers <see cref="long"/>, other numbers <see cref="double"/>, <c>null</c> stays <c>null</c>.
/// </remarks>
internal static class JsonScriptConverter
{
    /// <summary>
    /// Converts a JSON element and everything below it.
    /// </summary>
    /// <param name="element">The JSON value.</param>
    /// <param name="readOnly">
    /// Makes every converted object and array read-only, for values shared by pages that render in parallel.
    /// </param>
    public static object? ToScriptValue(JsonElement element, bool readOnly = false) => element.ValueKind switch
    {
        JsonValueKind.Object => ToScriptObject(element, readOnly),
        JsonValueKind.Array => ToScriptArray(element, readOnly),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => null
    };

    private static ScriptObject ToScriptObject(JsonElement element, bool readOnly)
    {
        var scriptObject = new ScriptObject();
        foreach (var property in element.EnumerateObject())
        {
            scriptObject[property.Name] = ToScriptValue(property.Value, readOnly);
        }

        scriptObject.IsReadOnly = readOnly;
        return scriptObject;
    }

    private static ScriptArray ToScriptArray(JsonElement element, bool readOnly)
    {
        var scriptArray = new ScriptArray();
        foreach (var item in element.EnumerateArray())
        {
            scriptArray.Add(ToScriptValue(item, readOnly));
        }

        scriptArray.IsReadOnly = readOnly;
        return scriptArray;
    }
}
