using System.Text.Json;

namespace Spectara.Revela.Sdk.Json;

/// <summary>
/// Centralized <see cref="JsonSerializerOptions"/> presets used across Revela.
/// </summary>
/// <remarks>
/// All user-facing JSON files (<c>project.json</c>, <c>site.json</c>, <c>theme.json</c>,
/// <c>images.json</c>, global <c>revela.json</c>) are treated as JSONC: line/block
/// comments and trailing commas are tolerated when reading.
///
/// Note: comments and original formatting are NOT preserved when Revela writes a file
/// back (e.g. <c>revela plugins add</c>). See docs/configuration.md.
/// </remarks>
public static class RevelaJsonOptions
{
    /// <summary>
    /// Lenient read options: skips comments and allows trailing commas.
    /// </summary>
    public static JsonSerializerOptions Lenient { get; } = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Pretty-printed write options used when Revela rewrites a user-facing JSON file.
    /// </summary>
    public static JsonSerializerOptions Write { get; } = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// <see cref="JsonDocumentOptions"/> mirroring <see cref="Lenient"/> for
    /// <see cref="JsonDocument.Parse(string, JsonDocumentOptions)"/> call sites.
    /// </summary>
    public static JsonDocumentOptions LenientDocument { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
