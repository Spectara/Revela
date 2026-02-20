namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Extended metadata for theme plugins — adds preview image and tags.
/// </summary>
/// <remarks>
/// Inherits from <see cref="PluginMetadata"/> (which is a record),
/// so it supports value equality, <c>with</c> expressions, and pattern matching.
/// </remarks>
public record ThemeMetadata : PluginMetadata
{
    /// <summary>URL to preview image of the theme.</summary>
    public Uri? PreviewImageUri { get; init; }

    /// <summary>Theme tags for discovery (e.g., "minimal", "dark", "gallery").</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
